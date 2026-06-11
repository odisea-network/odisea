using Odisea.Domain.Common;
using Odisea.Domain.Enums;
using Odisea.Domain.ValueObjects;

namespace Odisea.Domain.Entities;

public class Offer : Entity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public OwnerType OwnerType { get; set; }
    public Visibility Visibility { get; set; }

    public Guid? OwningAgencyId { get; set; }

    // Supply-side lineage. Null for agency-private or hand-seeded offers.
    public OfferSource? Source { get; set; }

    public string Country { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    public decimal Price { get; set; }
    public string Currency { get; set; } = "EUR";

    public BoardBasis BoardBasis { get; set; }
    public Transport Transport { get; set; }

    public int DurationNights { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public List<string> Tags { get; set; } = new();
    public string ImageUrl { get; set; } = string.Empty;

    public OfferStatus Status { get; set; } = OfferStatus.Draft;
}
