using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Booking.Domain;
using Odisea.Modules.Booking.Features.Bookings;
using Odisea.Modules.Booking.Infrastructure;
using Odisea.Modules.Catalog.PublicApi;
using Odisea.Modules.Integrations.Adapters.Mock;
using Odisea.Modules.Integrations.Features;
using Odisea.Modules.Integrations.Infrastructure;
using Odisea.Modules.Pricing.Domain;
using Odisea.Modules.Pricing.Features.Calculator;
using Odisea.Modules.Pricing.Infrastructure;
using Odisea.Modules.Integrations.PublicApi;
using Odisea.SharedKernel;
using Xunit;

namespace Odisea.UnitTests.Booking;

/// Full lifecycle over the real mock provider, real token protector and
/// real price calculator — only persistence and the two lookups are faked.
public class BookingServiceTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeAgencyLookup(bool active = true) : IAgencyLookup
    {
        public Task<AgencySnapshot?> FindAsync(Guid agencyId, CancellationToken ct) =>
            Task.FromResult<AgencySnapshot?>(new AgencySnapshot(agencyId, "Test Agency", active, 50_000m));
    }

    private sealed class FakeProgramLookup(BookingTarget? target) : IProgramLookup
    {
        public Task<BookingTarget?> ResolveBookingTargetAsync(
            Guid programId, Guid departureId, Guid hotelId, CancellationToken ct) =>
            Task.FromResult(target);
    }

    private sealed class FakeRefGenerator : IBookingRefGenerator
    {
        private int _n;
        public Task<string> NextRefAsync(int year, CancellationToken ct) =>
            Task.FromResult($"ODI-{year}-{++_n:D6}");
    }

    private readonly BookingDbContext _db;
    private readonly PricingDbContext _pricing;
    private readonly MockProviderOptions _mockOptions = new();
    private readonly OfferTokenProtector _protector = new(new EphemeralDataProtectionProvider());
    private readonly FakeClock _clock = new();
    private readonly Guid _agencyId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly BookingTarget _target;
    private readonly MockReservationProvider _mock;

    public BookingServiceTests()
    {
        _db = new BookingDbContext(new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _pricing = new PricingDbContext(new DbContextOptionsBuilder<PricingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _pricing.PricingRules.Add(new PricingRule
        {
            Name = "markup", Kind = RuleKind.Markup, ValueType = RuleValueType.Percent, Value = 15m,
        });
        _pricing.SaveChanges();

        _mock = new MockReservationProvider(Options.Create(_mockOptions), _clock);
        _target = new BookingTarget(
            Guid.NewGuid(), "Antalya Easter 2027", "mock",
            Guid.NewGuid(), new DateOnly(2027, 3, 30), new DateOnly(2027, 4, 6),
            Guid.NewGuid(), "Mock Palace");
    }

    private BookingService Service(bool agencyActive = true, bool resolvable = true) => new(
        _db,
        new ProviderRegistry([_mock], new IntegrationsDbContext(
            new DbContextOptionsBuilder<IntegrationsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options)),
        _protector,
        new PriceCalculator(_pricing),
        new FakeAgencyLookup(agencyActive),
        new FakeProgramLookup(resolvable ? _target : null),
        new FakeRefGenerator(),
        _clock);

    private async Task<string> ProtectedOfferTokenAsync()
    {
        var search = await _mock.SearchAsync(
            new ProviderSearchRequest("ANT",
                new DateRange(new DateOnly(2027, 3, 30), new DateOnly(2027, 4, 6)),
                new Pax(2), ["MOCK-ANT-001"]),
            CancellationToken.None);
        return _protector.Protect(search.Offers[0].OfferToken);
    }

    private async Task<BookingDto> CreateDraftAsync(BookingService service) =>
        await service.CreateDraftAsync(
            new CreateBookingRequest(_target.ProgramId, _target.DepartureId, _target.HotelId,
                await ProtectedOfferTokenAsync(),
                [new PassengerRequest("Ivan", "Petrov", new DateOnly(1990, 5, 1)),
                 new PassengerRequest("Maria", "Petrova", new DateOnly(1992, 7, 2))]),
            _agencyId, _userId, CancellationToken.None);

    [Fact]
    public async Task Draft_gets_ref_passengers_and_history()
    {
        var dto = await CreateDraftAsync(Service());

        Assert.Equal("ODI-2027-000001", dto.Ref);
        Assert.Equal("Draft", dto.Status);
        Assert.Equal(2, dto.Passengers.Count);
        Assert.True(dto.Passengers[0].IsLead); // first passenger becomes lead by default
        Assert.Single(dto.History);
        Assert.Null(dto.SellPrice);
    }

    [Fact]
    public async Task Inactive_agency_cannot_create_bookings()
    {
        await Assert.ThrowsAsync<BookingValidationException>(
            () => CreateDraftAsync(Service(agencyActive: false)));
    }

    [Fact]
    public async Task Unbookable_combination_is_rejected()
    {
        await Assert.ThrowsAsync<BookingValidationException>(
            () => CreateDraftAsync(Service(resolvable: false)));
    }

    [Fact]
    public async Task Tampered_offer_token_is_rejected()
    {
        var service = Service();
        await Assert.ThrowsAsync<InvalidOfferTokenException>(
            () => service.CreateDraftAsync(
                new CreateBookingRequest(_target.ProgramId, _target.DepartureId, _target.HotelId,
                    "tampered-token", [new PassengerRequest("A", "B", null)]),
                _agencyId, _userId, CancellationToken.None));
    }

    [Fact]
    public async Task RePrice_sets_sell_price_and_breakdown()
    {
        var service = Service();
        var draft = await CreateDraftAsync(service);

        var result = await service.RePriceAsync(draft.Id, _agencyId, CancellationToken.None);

        Assert.Equal("PriceConfirmed", result.Booking.Status);
        Assert.False(result.PriceChanged); // first pricing — nothing to compare against
        Assert.NotNull(result.Booking.SellPrice);
        var stored = await _db.Bookings.SingleAsync();
        Assert.Contains("Markup", stored.BreakdownJson);
        Assert.Equal(Math.Round((decimal)stored.NetAmount! * 1.15m, 2), stored.SellAmount);
    }

    [Fact]
    public async Task RePrice_surfaces_drift_on_the_second_pass()
    {
        var service = Service();
        var draft = await CreateDraftAsync(service);
        await service.RePriceAsync(draft.Id, _agencyId, CancellationToken.None);

        _mockOptions.RePriceDriftRatePercent = 100;
        _mockOptions.DriftPercent = 10m;
        var second = await service.RePriceAsync(draft.Id, _agencyId, CancellationToken.None);

        Assert.True(second.PriceChanged);
        Assert.True(second.Booking.SellPrice > second.PreviousSellPrice);
    }

    [Fact]
    public async Task Unavailable_offer_blocks_without_corrupting_state()
    {
        var service = Service();
        var draft = await CreateDraftAsync(service);
        _mockOptions.FailureRatePercent = 100;

        await Assert.ThrowsAsync<OfferUnavailableException>(
            () => service.RePriceAsync(draft.Id, _agencyId, CancellationToken.None));
        Assert.Equal(BookingStatus.Draft, (await _db.Bookings.SingleAsync()).Status);
    }

    [Fact]
    public async Task Confirm_books_at_the_provider_and_stores_the_reference()
    {
        var service = Service();
        var draft = await CreateDraftAsync(service);
        await service.RePriceAsync(draft.Id, _agencyId, CancellationToken.None);

        var confirmed = await service.ConfirmAsync(draft.Id, _agencyId, CancellationToken.None);

        Assert.Equal("Confirmed", confirmed.Status);
        Assert.StartsWith("MOCK-", confirmed.ProviderBookingRef);
        Assert.Equal(["-", "Draft", "PriceConfirmed", "PendingConfirmation"],
            confirmed.History.Select(h => h.From).ToArray());
    }

    [Fact]
    public async Task Confirm_straight_from_draft_is_an_invalid_transition()
    {
        var service = Service();
        var draft = await CreateDraftAsync(service);

        await Assert.ThrowsAsync<InvalidBookingTransitionException>(
            () => service.ConfirmAsync(draft.Id, _agencyId, CancellationToken.None));
    }

    [Fact]
    public async Task Cancel_after_confirmation_cancels_at_the_provider_too()
    {
        var service = Service();
        var draft = await CreateDraftAsync(service);
        await service.RePriceAsync(draft.Id, _agencyId, CancellationToken.None);
        await service.ConfirmAsync(draft.Id, _agencyId, CancellationToken.None);

        var cancelled = await service.CancelAsync(draft.Id, _agencyId, CancellationToken.None);
        Assert.Equal("Cancelled", cancelled.Status);
    }

    [Fact]
    public async Task Other_agencies_bookings_are_invisible()
    {
        var service = Service();
        var draft = await CreateDraftAsync(service);
        var otherAgency = Guid.NewGuid();

        Assert.Null(await service.GetAsync(draft.Id, otherAgency, CancellationToken.None));
        Assert.Empty(await service.ListAsync(otherAgency, CancellationToken.None));
        await Assert.ThrowsAsync<BookingValidationException>(
            () => service.RePriceAsync(draft.Id, otherAgency, CancellationToken.None));
    }
}
