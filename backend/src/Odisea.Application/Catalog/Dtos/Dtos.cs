using Odisea.Domain.Entities;
using Odisea.Domain.ValueObjects;

namespace Odisea.Application.Catalog.Dtos;

public record OfferDto(
    Guid Id,
    string Title,
    string Description,
    string Country,
    string City,
    decimal Price,
    string Currency,
    string BoardBasis,
    string Transport,
    int DurationNights,
    DateOnly? StartDate,
    DateOnly? EndDate,
    IReadOnlyList<string> Tags,
    string ImageUrl,
    string Visibility,
    string OwnerType,
    OfferSourceDto? Source,
    IReadOnlyList<PriceVariantDto> PriceVariants
);

public record OfferSourceDto(
    string Supplier,
    string ExternalId,
    DateTime? LastImportedAt,
    string State
);

// Operator manual-entry payload. Ownership (operator id), OwnerType, Visibility
// and Status are set server-side from the caller's context — not the request.
public record CreateOfferRequest(
    string Title,
    string Description,
    string Country,
    string City,
    decimal Price,
    string Currency,
    string BoardBasis,
    string Transport,
    int DurationNights,
    DateOnly? StartDate,
    DateOnly? EndDate,
    List<string>? Tags,
    string? ImageUrl
);

public record UpdateOfferRequest(
    string Title,
    string Description,
    string Country,
    string City,
    decimal Price,
    string Currency,
    string BoardBasis,
    string Transport,
    int DurationNights,
    DateOnly? StartDate,
    DateOnly? EndDate,
    List<string>? Tags,
    string? ImageUrl
);

// One row of the supplier's pricing matrix. Any dimension may be null when
// the supplier hasn't broken pricing down on that axis.
public record PriceVariantDto(
    Guid Id,
    DateOnly? DepartureDate,
    int? DurationNights,
    string? BoardBasis,
    int? Occupancy,
    decimal Price,
    string Currency
);

public record CollectionDto(
    Guid Id,
    Guid AgencyId,
    string Name,
    string Slug,
    string Status,
    FilterSpec Filter,
    IReadOnlyList<Guid> PinnedOfferIds,
    IReadOnlyList<Guid> ExcludedOfferIds,
    SortSpec Sort
);

public record CreateCollectionRequest(
    Guid AgencyId,
    string Name,
    string Slug,
    FilterSpec Filter,
    SortSpec? Sort,
    List<Guid>? PinnedOfferIds,
    List<Guid>? ExcludedOfferIds
);

public static class Mappings
{
    // supplierNames maps a SupplierConnectionId to its display name; pass the lookup
    // built for the result set so source.supplier resolves without a per-offer query.
    public static OfferDto ToDto(this Offer o, IReadOnlyDictionary<Guid, string>? supplierNames = null) => new(
        o.Id, o.Title, o.Description, o.Country, o.City, o.Price, o.Currency,
        o.BoardBasis.ToString(), o.Transport.ToString(), o.DurationNights,
        o.StartDate, o.EndDate, o.Tags, o.ImageUrl,
        o.Visibility.ToString(), o.OwnerType.ToString(),
        o.Source.ToDto(supplierNames),
        o.PriceVariants.Select(v => v.ToDto()).ToList());

    public static PriceVariantDto ToDto(this PriceVariant v) => new(
        v.Id, v.DepartureDate, v.DurationNights, v.BoardBasis?.ToString(),
        v.Occupancy, v.Price, v.Currency);

    private static OfferSourceDto? ToDto(this OfferSource? s, IReadOnlyDictionary<Guid, string>? supplierNames)
    {
        if (s is null) return null;
        var supplier = supplierNames is not null && supplierNames.TryGetValue(s.SupplierConnectionId, out var name)
            ? name
            : string.Empty;
        return new OfferSourceDto(supplier, s.ExternalId, s.LastImportedAt, s.ImportState.ToString());
    }

    public static CollectionDto ToDto(this Collection c) => new(
        c.Id, c.AgencyId, c.Name, c.Slug, c.Status.ToString(),
        c.Filter, c.PinnedOfferIds, c.ExcludedOfferIds, c.Sort);
}
