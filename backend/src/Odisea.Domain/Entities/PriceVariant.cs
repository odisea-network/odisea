using Odisea.Domain.Common;
using Odisea.Domain.Enums;

namespace Odisea.Domain.Entities;

// A single priced row from the supplier's pricing matrix.
//
// Real hotel offers have many of these — a 7-night escape can have 30+ rows
// across departures, board options and occupancies. Five dimensions cover the
// shapes we've actually seen in PeakView listings; any of them may be null to
// mean "any / not specified" so a supplier can publish whatever level of
// granularity they actually have.
//
// `Offer.Price` continues to hold the "from" price (lowest variant) — the
// filter engine and component grids read from there, so we don't take a query
// regression on day one. Variants are exposed on `OfferDto.PriceVariants` for
// callers (offer detail screens, price tables) that want to render the matrix.
public class PriceVariant : Entity
{
    public Guid OfferId { get; set; }
    public Offer Offer { get; set; } = null!;

    public DateOnly? DepartureDate { get; set; }
    public int? DurationNights { get; set; }
    public BoardBasis? BoardBasis { get; set; }
    public int? Occupancy { get; set; }

    public decimal Price { get; set; }
    public string Currency { get; set; } = "EUR";
}
