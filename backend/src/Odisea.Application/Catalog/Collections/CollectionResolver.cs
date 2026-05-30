using Microsoft.EntityFrameworkCore;
using Odisea.Application.Catalog.Filtering;
using Odisea.Domain.Entities;
using Odisea.Domain.ValueObjects;

namespace Odisea.Application.Catalog.Collections;

// Resolves a Collection to its matching, ordered list of Offers:
//   1. Filter via FilterSpec (whitelisted only).
//   2. Remove ExcludedOfferIds.
//   3. Sort.
//   4. Prepend PinnedOfferIds (ordered as given), removing duplicates from the rest.
public static class CollectionResolver
{
    public static async Task<List<Offer>> ResolveAsync(
        Collection collection,
        IQueryable<Offer> offersQuery,
        CancellationToken ct)
    {
        var filtered = FilterResolver.Apply(offersQuery, collection.Filter);

        if (collection.ExcludedOfferIds.Count > 0)
        {
            var ex = collection.ExcludedOfferIds;
            filtered = filtered.Where(o => !ex.Contains(o.Id));
        }

        filtered = ApplySort(filtered, collection.Sort);

        var primary = await filtered.ToListAsync(ct);

        if (collection.PinnedOfferIds.Count == 0)
            return primary;

        var pinnedSet = collection.PinnedOfferIds.ToHashSet();
        var pinned = await offersQuery.Where(o => pinnedSet.Contains(o.Id)).ToListAsync(ct);
        // Preserve the order declared in PinnedOfferIds.
        var pinnedOrdered = collection.PinnedOfferIds
            .Select(id => pinned.FirstOrDefault(p => p.Id == id))
            .Where(p => p is not null)
            .Cast<Offer>()
            .ToList();

        var rest = primary.Where(o => !pinnedSet.Contains(o.Id));
        return pinnedOrdered.Concat(rest).ToList();
    }

    private static IQueryable<Offer> ApplySort(IQueryable<Offer> q, SortSpec sort)
    {
        var desc = string.Equals(sort.Direction, "desc", StringComparison.OrdinalIgnoreCase);
        return sort.Field.ToLowerInvariant() switch
        {
            "price" => desc ? q.OrderByDescending(o => o.Price) : q.OrderBy(o => o.Price),
            "title" => desc ? q.OrderByDescending(o => o.Title) : q.OrderBy(o => o.Title),
            "createdat" or "created" => desc ? q.OrderByDescending(o => o.CreatedAt) : q.OrderBy(o => o.CreatedAt),
            "duration" or "durationnights" => desc ? q.OrderByDescending(o => o.DurationNights) : q.OrderBy(o => o.DurationNights),
            _ => q.OrderBy(o => o.Price),
        };
    }
}
