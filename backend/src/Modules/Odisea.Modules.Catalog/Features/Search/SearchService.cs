using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Odisea.Modules.Catalog.Domain;
using Odisea.Modules.Catalog.Infrastructure;
using Odisea.Modules.Integrations.PublicApi;
using Odisea.Modules.Pricing.PublicApi;
using Odisea.SharedKernel;

namespace Odisea.Modules.Catalog.Features.Search;

public sealed class SearchNotFoundException(string message) : DomainException(message);

public class SearchService(
    CatalogDbContext db,
    IProviderRegistry providers,
    IPriceCalculator priceCalculator,
    IOfferTokenProtector tokenProtector,
    ILogger<SearchService> logger)
{
    public async Task<SearchResponse> SearchAsync(SearchRequest request, Guid agencyId, CancellationToken ct)
    {
        var program = await db.Programs
            .Include(p => p.Departures)
            .Include(p => p.Hotels)
            .FirstOrDefaultAsync(p => p.Id == request.ProgramId && p.Status == ProgramStatus.Published, ct)
            ?? throw new SearchNotFoundException("Program not found or not published.");

        var departure = program.Departures.FirstOrDefault(d => d.Id == request.DepartureId)
            ?? throw new SearchNotFoundException("Departure not found in this program.");

        var locationMapping = await db.LocationMappings.FirstOrDefaultAsync(
                m => m.DestinationId == program.DestinationId && m.ProviderCode == program.ProviderCode, ct)
            ?? throw new MissingMappingException(
                $"destination {program.DestinationId} has no location code for provider '{program.ProviderCode}'");

        var allowedHotelIds = program.Hotels.Select(h => h.HotelId).ToList();
        var mappings = await db.HotelMappings
            .Where(m => allowedHotelIds.Contains(m.HotelId) && m.ProviderCode == program.ProviderCode)
            .ToDictionaryAsync(m => m.ExternalHotelCode, m => m.HotelId, ct);

        if (mappings.Count < allowedHotelIds.Count)
            logger.LogWarning(
                "Program {ProgramId}: {Unmapped} of {Total} allowed hotels have no '{Provider}' mapping and are excluded",
                program.Id, allowedHotelIds.Count - mappings.Count, allowedHotelIds.Count, program.ProviderCode);

        var hotels = await db.Hotels
            .Where(h => allowedHotelIds.Contains(h.Id))
            .ToDictionaryAsync(h => h.Id, ct);

        var provider = providers.Get(program.ProviderCode);
        var result = await provider.SearchAsync(
            new ProviderSearchRequest(
                locationMapping.ExternalLocationCode,
                departure.Period,
                new Pax(request.Adults, request.Children),
                [.. mappings.Keys]),
            ct);

        List<HotelOfferDto> offers = [];
        foreach (var offer in result.Offers)
        {
            if (!mappings.TryGetValue(offer.ExternalHotelCode, out var hotelId))
            {
                // Never show a result we cannot attribute to our own master data.
                logger.LogWarning("Dropping unmapped provider hotel code {Code}", offer.ExternalHotelCode);
                continue;
            }

            var breakdown = await priceCalculator.CalculateSellPriceAsync(
                new PricingContext(offer.NetPrice, program.Id, agencyId, departure.StartDate), ct);

            var hotel = hotels[hotelId];
            offers.Add(new HotelOfferDto(
                hotelId, hotel.Name, hotel.Stars,
                offer.RoomType, offer.Board,
                breakdown.SellPrice.Amount, breakdown.SellPrice.Currency,
                breakdown.AgencyCommission.Amount,
                // Provider tokens can embed the net cost — never hand them out raw.
                tokenProtector.Protect(offer.OfferToken),
                offer.OfferExpiresAt));
        }

        return new SearchResponse(
            program.Id, program.Name, departure.Id,
            departure.StartDate, departure.EndDate, departure.Period.Nights,
            [.. offers.OrderBy(o => o.SellPrice)]);
    }
}
