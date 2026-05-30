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
    string OwnerType
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
    public static OfferDto ToDto(this Offer o) => new(
        o.Id, o.Title, o.Description, o.Country, o.City, o.Price, o.Currency,
        o.BoardBasis.ToString(), o.Transport.ToString(), o.DurationNights,
        o.StartDate, o.EndDate, o.Tags, o.ImageUrl,
        o.Visibility.ToString(), o.OwnerType.ToString());

    public static CollectionDto ToDto(this Collection c) => new(
        c.Id, c.AgencyId, c.Name, c.Slug, c.Status.ToString(),
        c.Filter, c.PinnedOfferIds, c.ExcludedOfferIds, c.Sort);
}
