using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Odisea.Modules.Catalog.Domain;
using Odisea.Modules.Catalog.Features.Search;
using Odisea.Modules.Catalog.Infrastructure;
using Odisea.Modules.Integrations.Adapters.Mock;
using Odisea.Modules.Integrations.Features;
using Odisea.Modules.Integrations.Infrastructure;
using Odisea.Modules.Pricing.Domain;
using Odisea.Modules.Pricing.Features.Calculator;
using Odisea.Modules.Pricing.Infrastructure;
using Odisea.SharedKernel;
using Xunit;
using CatalogProgram = Odisea.Modules.Catalog.Domain.Program;

namespace Odisea.UnitTests.Catalog;

/// Wires the real mock provider, real registry and real calculator over
/// InMemory contexts — the full search path short of HTTP.
public class SearchServiceTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 10, 9, 10, 0, 0, TimeSpan.Zero);
    }

    private readonly CatalogDbContext _catalog;
    private readonly PricingDbContext _pricing;
    private readonly SearchService _service;
    private readonly Guid _agencyId = Guid.NewGuid();

    private CatalogProgram _program = null!;
    private ProgramDeparture _departure = null!;
    private Hotel _mappedHotel = null!;

    public SearchServiceTests()
    {
        _catalog = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _pricing = new PricingDbContext(new DbContextOptionsBuilder<PricingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var integrations = new IntegrationsDbContext(new DbContextOptionsBuilder<IntegrationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var mock = new MockReservationProvider(Options.Create(new MockProviderOptions()), new FakeClock());
        _service = new SearchService(
            _catalog,
            new ProviderRegistry([mock], integrations),
            new PriceCalculator(_pricing),
            new OfferTokenProtector(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider()),
            NullLogger<SearchService>.Instance);
    }

    private void SeedProgram(bool mapSecondHotel = true, bool mapLocation = true, bool publish = true)
    {
        var destination = new Destination { Name = "Antalya", Country = "TR" };
        _catalog.Destinations.Add(destination);
        if (mapLocation)
            _catalog.LocationMappings.Add(new LocationMapping
            {
                DestinationId = destination.Id,
                ProviderCode = "mock",
                ExternalLocationCode = "ANT",
            });

        _mappedHotel = new Hotel { Name = "Mock Palace", DestinationId = destination.Id, Stars = 5 };
        var unmapped = new Hotel { Name = "No Mapping Inn", DestinationId = destination.Id, Stars = 3 };
        _catalog.Hotels.AddRange(_mappedHotel, unmapped);
        _catalog.HotelMappings.Add(new HotelMapping
        {
            HotelId = _mappedHotel.Id,
            ProviderCode = "mock",
            ExternalHotelCode = "MOCK-ANT-001",
        });
        if (mapSecondHotel)
            _catalog.HotelMappings.Add(new HotelMapping
            {
                HotelId = unmapped.Id,
                ProviderCode = "mock",
                ExternalHotelCode = "MOCK-ANT-002",
            });

        _program = new CatalogProgram
        {
            Name = "Antalya Easter 2027",
            DestinationId = destination.Id,
            ProviderCode = "mock",
        };
        _departure = new ProgramDeparture
        {
            ProgramId = _program.Id,
            StartDate = new DateOnly(2027, 3, 30),
            EndDate = new DateOnly(2027, 4, 6),
        };
        _program.Departures.Add(_departure);
        _program.Hotels.Add(new ProgramHotel { ProgramId = _program.Id, HotelId = _mappedHotel.Id });
        _program.Hotels.Add(new ProgramHotel { ProgramId = _program.Id, HotelId = unmapped.Id });
        if (publish)
            _program.Publish();
        _catalog.Programs.Add(_program);
        _catalog.SaveChanges();
    }

    private Task<SearchResponse> SearchAsync() => _service.SearchAsync(
        new SearchRequest(_program.Id, _departure.Id, 2, 0), _agencyId, CancellationToken.None);

    [Fact]
    public async Task Returns_marked_up_offers_sorted_by_price_and_never_the_net()
    {
        SeedProgram();
        _pricing.PricingRules.AddRange(
            new PricingRule { Name = "markup", Kind = RuleKind.Markup, ValueType = RuleValueType.Percent, Value = 20m },
            new PricingRule { Name = "commission", Kind = RuleKind.Commission, ValueType = RuleValueType.Percent, Value = 10m });
        _pricing.SaveChanges();

        var response = await SearchAsync();

        Assert.Equal(4, response.Offers.Count); // 2 hotels x HB/AI
        Assert.Equal(7, response.Nights);
        Assert.Equal(response.Offers.OrderBy(o => o.SellPrice).Select(o => o.SellPrice), response.Offers.Select(o => o.SellPrice));
        Assert.All(response.Offers, o =>
        {
            Assert.Equal("EUR", o.Currency);
            Assert.Equal(Math.Round(o.SellPrice * 0.10m, 2), o.AgencyCommission);
            Assert.NotEmpty(o.OfferToken);
        });
    }

    [Fact]
    public async Task Offer_tokens_do_not_leak_the_net_cost()
    {
        SeedProgram();
        var response = await SearchAsync();

        foreach (var offer in response.Offers)
        {
            // The raw mock token is base64 JSON containing "Net": the protected
            // token must not be decodable into anything readable.
            var decodable = false;
            try
            {
                var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(offer.OfferToken));
                decodable = decoded.Contains("Net", StringComparison.OrdinalIgnoreCase);
            }
            catch (FormatException) { /* not base64 at all — good */ }
            catch (ArgumentException) { /* invalid utf8 — good */ }
            Assert.False(decodable, "Offer token exposed provider payload");
        }
    }

    [Fact]
    public async Task Hotels_without_a_mapping_are_dropped_not_shown()
    {
        SeedProgram(mapSecondHotel: false);
        var response = await SearchAsync();

        Assert.Equal(2, response.Offers.Count); // only the mapped hotel's HB/AI
        Assert.All(response.Offers, o => Assert.Equal(_mappedHotel.Id, o.HotelId));
    }

    [Fact]
    public async Task Unpublished_program_is_invisible_to_search()
    {
        SeedProgram(publish: false);
        await Assert.ThrowsAsync<SearchNotFoundException>(SearchAsync);
    }

    [Fact]
    public async Task Missing_location_mapping_is_a_configuration_error()
    {
        SeedProgram(mapLocation: false);
        await Assert.ThrowsAsync<MissingMappingException>(SearchAsync);
    }
}
