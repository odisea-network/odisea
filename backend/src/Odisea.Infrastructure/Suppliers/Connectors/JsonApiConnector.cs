using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Common.Interfaces;
using Odisea.Application.Suppliers.Connectors;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Domain.ValueObjects;

namespace Odisea.Infrastructure.Suppliers.Connectors;

// Pulls offers from a supplier's JSON HTTP endpoint and upserts them into the
// catalog. The endpoint must return a JSON array of objects in the canonical
// offer shape (see SupplierOfferPayload). Each item is keyed by externalId:
// the SourceOffer row (raw payload + freshness) and the normalized Offer are
// upserted together, so re-running is idempotent and edits flow through.
//
// Staleness/deactivation is deliberately NOT done here — the scheduler runs the
// freshness sweep right after, which expires offers whose LastSeenAt aged out.
public sealed class JsonApiConnector(HttpClient http, IAppDbContext db) : IConnector
{
    public SupplierConnectionKind Kind => SupplierConnectionKind.JsonApi;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<ConnectorRunResult> RunAsync(SupplierConnection connection, CancellationToken ct)
    {
        var config = ReadConfig(connection.ConfigJson);
        if (config is null || string.IsNullOrWhiteSpace(config.Url))
            return ConnectorRunResult.Empty("Connection config is missing a 'url'.");

        JsonElement[] items;
        try
        {
            var body = await http.GetStringAsync(config.Url, ct);
            items = JsonSerializer.Deserialize<JsonElement[]>(body, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            // A failed fetch/parse is a failed run, not an exception — ImportRunner
            // records it and leaves the last good freshness intact.
            return ConnectorRunResult.Empty($"Fetch failed: {ex.Message}");
        }

        var now = DateTime.UtcNow;
        var imported = 0;

        foreach (var element in items)
        {
            ct.ThrowIfCancellationRequested();

            var item = Deserialize(element);
            if (item is null || !item.IsValid(out var board, out var transport))
            {
                // A bad row is skipped (not a run-level failure, so freshness still
                // advances), but its SourceOffer is stamped Failed for traceability.
                if (item is { ExternalId.Length: > 0 })
                    await UpsertSource(connection.Id, item.ExternalId, element.GetRawText(), ImportState.Failed, now, ct);
                continue;
            }

            await UpsertSource(connection.Id, item.ExternalId, element.GetRawText(), ImportState.Imported, now, ct);
            await UpsertOffer(connection, item, board, transport, now, ct);
            imported++;
        }

        await db.SaveChangesAsync(ct);
        return new ConnectorRunResult(items.Length, imported, 0, [], now);
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
        SupplierConnection connection, SupplierOfferPayload item,
        BoardBasis board, Transport transport, DateTime now, CancellationToken ct)
    {
        var offer = await db.Offers.FirstOrDefaultAsync(
            o => o.Source != null
                 && o.Source.SupplierConnectionId == connection.Id
                 && o.Source.ExternalId == item.ExternalId, ct);

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
                    ExternalId = item.ExternalId,
                },
            };
            db.Offers.Add(offer);
        }

        offer.Title = item.Title;
        offer.Description = item.Description ?? string.Empty;
        offer.Country = item.Country;
        offer.City = item.City ?? string.Empty;
        offer.Price = item.Price;
        offer.Currency = string.IsNullOrWhiteSpace(item.Currency) ? "EUR" : item.Currency;
        offer.BoardBasis = board;
        offer.Transport = transport;
        offer.DurationNights = item.Nights;
        offer.Tags = item.Tags ?? [];
        offer.ImageUrl = item.ImageUrl ?? string.Empty;
        // Imported offers go live immediately — a connector feed is trusted supply,
        // not a draft awaiting review. Operators can unpublish individually.
        offer.Status = OfferStatus.Published;
        offer.Source!.ImportState = ImportState.Imported;
        offer.Source.LastImportedAt = now;
        offer.UpdatedAt = now;
    }

    private static SupplierConnectionConfig? ReadConfig(string configJson)
    {
        try { return JsonSerializer.Deserialize<SupplierConnectionConfig>(configJson, JsonOptions); }
        catch (JsonException) { return null; }
    }

    private static SupplierOfferPayload? Deserialize(JsonElement element)
    {
        try { return element.Deserialize<SupplierOfferPayload>(JsonOptions); }
        catch (JsonException) { return null; }
    }

    private sealed record SupplierConnectionConfig(string? Url);

    // Canonical supplier offer shape (integration contract v1). board/transport are
    // strings parsed against the domain enums; unknown values mark the row invalid.
    private sealed record SupplierOfferPayload(
        string ExternalId,
        string Title,
        string? Description,
        string Country,
        string? City,
        decimal Price,
        string? Currency,
        string? Board,
        string? Transport,
        int Nights,
        string? ImageUrl,
        List<string>? Tags)
    {
        public bool IsValid(out BoardBasis board, out Transport transport)
        {
            board = default;
            transport = default;
            return !string.IsNullOrWhiteSpace(ExternalId)
                && !string.IsNullOrWhiteSpace(Title)
                && !string.IsNullOrWhiteSpace(Country)
                && Price >= 0
                && Enum.TryParse(Board, ignoreCase: true, out board)
                && Enum.TryParse(Transport, ignoreCase: true, out transport);
        }
    }
}
