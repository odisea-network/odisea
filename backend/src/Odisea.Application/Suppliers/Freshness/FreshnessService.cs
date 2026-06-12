using Microsoft.EntityFrameworkCore;
using Odisea.Application.Common.Interfaces;
using Odisea.Domain.Enums;

namespace Odisea.Application.Suppliers.Freshness;

public sealed class FreshnessService(IAppDbContext db) : IFreshnessService
{
    public async Task<FreshnessSweepResult> SweepAsync(Guid supplierConnectionId, CancellationToken ct)
    {
        var connection = await db.SupplierConnections
            .FirstOrDefaultAsync(c => c.Id == supplierConnectionId, ct)
            ?? throw new InvalidOperationException($"SupplierConnection {supplierConnectionId} not found.");

        var cutoff = DateTime.UtcNow.AddHours(-connection.FreshnessTtlHours);

        // Source records not seen since the cutoff go Stale (skip ones already Stale
        // or Failed — only Imported/Pending rows can decay).
        var staleSources = await db.SourceOffers
            .Where(s => s.SupplierConnectionId == supplierConnectionId
                        && s.LastSeenAt < cutoff
                        && (s.State == ImportState.Imported || s.State == ImportState.Pending))
            .ToListAsync(ct);

        foreach (var s in staleSources)
        {
            s.State = ImportState.Stale;
            s.UpdatedAt = DateTime.UtcNow;
        }

        // Propagate to the normalized offers that came from this connection and have
        // gone stale. Offer.Source is an owned value object flattened into source_*
        // columns, so we match on the connection + the source external id.
        var staleExternalIds = staleSources.Select(s => s.ExternalId).ToHashSet();

        var offersToExpire = staleExternalIds.Count == 0
            ? []
            : await db.Offers
                .Where(o => o.Source != null
                            && o.Source.SupplierConnectionId == supplierConnectionId
                            && o.Source.ImportState != ImportState.Stale
                            && staleExternalIds.Contains(o.Source.ExternalId))
                .ToListAsync(ct);

        foreach (var o in offersToExpire)
        {
            o.Source!.ImportState = ImportState.Stale;
            o.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return new FreshnessSweepResult(staleSources.Count, offersToExpire.Count);
    }
}
