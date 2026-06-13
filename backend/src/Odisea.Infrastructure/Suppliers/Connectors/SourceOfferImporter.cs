using Microsoft.EntityFrameworkCore;
using Odisea.Application.Common.Interfaces;
using Odisea.Application.Suppliers.Connectors;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Domain.ValueObjects;

namespace Odisea.Infrastructure.Suppliers.Connectors;

// Shared import core for fetch-based connectors. Given a batch of RawOffers from a
// connection, it upserts each by externalId — both the SourceOffer (raw payload +
// freshness) and the normalized Offer — so re-running is idempotent.
//
// A malformed row is skipped (its SourceOffer marked Failed) rather than failing
// the whole batch, so freshness still advances; the result carries no run-level
// errors. Deactivation of vanished offers is the freshness sweep's job, not this.
public sealed class SourceOfferImporter(IAppDbContext db)
{
    public async Task<ConnectorRunResult> ImportAsync(
        SupplierConnection connection, IReadOnlyList<RawOffer> rawOffers, DateTime now, CancellationToken ct)
    {
        var imported = 0;

        foreach (var raw in rawOffers)
        {
            ct.ThrowIfCancellationRequested();

            if (!raw.IsValid(out var board, out var transport))
            {
                if (!string.IsNullOrWhiteSpace(raw.ExternalId))
                    await UpsertSource(connection.Id, raw.ExternalId, raw.RawPayload, ImportState.Failed, now, ct);
                continue;
            }

            await UpsertSource(connection.Id, raw.ExternalId, raw.RawPayload, ImportState.Imported, now, ct);
            await UpsertOffer(connection, raw, board, transport, now, ct);
            imported++;
        }

        await db.SaveChangesAsync(ct);
        return new ConnectorRunResult(rawOffers.Count, imported, 0, [], now);
    }

    private async Task UpsertSource(
        Guid connectionId, string externalId, string rawPayload, ImportState state, DateTime now, CancellationToken ct)
    {
        var source = await db.SourceOffers
            .FirstOrDefaultAsync(s => s.SupplierConnectionId == connectionId && s.ExternalId == externalId, ct);

        if (source is null)
        {
            source = new SourceOffer
            {
                SupplierConnectionId = connectionId,
                ExternalId = externalId,
                FirstSeenAt = now,
            };
            db.SourceOffers.Add(source);
        }

        source.RawPayload = rawPayload;
        source.State = state;
        source.LastSeenAt = now;
        source.UpdatedAt = now;
    }

    private async Task UpsertOffer(
        SupplierConnection connection, RawOffer raw,
        BoardBasis board, Transport transport, DateTime now, CancellationToken ct)
    {
        var offer = await db.Offers.FirstOrDefaultAsync(
            o => o.Source != null
                 && o.Source.SupplierConnectionId == connection.Id
                 && o.Source.ExternalId == raw.ExternalId, ct);

        if (offer is null)
        {
            offer = new Offer
            {
                OwnerType = OwnerType.Operator,
                Visibility = Visibility.PlatformShared,
                OwningOperatorId = connection.OperatorId,
                Source = new OfferSource
                {
                    SupplierConnectionId = connection.Id,
                    ExternalId = raw.ExternalId,
                },
            };
            db.Offers.Add(offer);
        }

        offer.Title = raw.Title;
        offer.Description = raw.Description ?? string.Empty;
        offer.Country = raw.Country;
        offer.City = raw.City ?? string.Empty;
        offer.Price = raw.Price;
        offer.Currency = string.IsNullOrWhiteSpace(raw.Currency) ? "EUR" : raw.Currency;
        offer.BoardBasis = board;
        offer.Transport = transport;
        offer.DurationNights = raw.Nights;
        offer.Tags = raw.Tags is null ? [] : [.. raw.Tags];
        offer.ImageUrl = raw.ImageUrl ?? string.Empty;
        // Imported offers go live immediately — a connector feed is trusted supply,
        // not a draft awaiting review. Operators can unpublish individually.
        offer.Status = OfferStatus.Published;
        offer.Source!.ImportState = ImportState.Imported;
        offer.Source.LastImportedAt = now;
        offer.UpdatedAt = now;
    }
}
