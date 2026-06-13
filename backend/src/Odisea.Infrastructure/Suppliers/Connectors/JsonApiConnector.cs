using System.Text.Json;
using Odisea.Application.Suppliers.Connectors;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;

namespace Odisea.Infrastructure.Suppliers.Connectors;

// Pulls offers from a supplier's JSON HTTP endpoint. The endpoint must return a
// JSON array of objects in the canonical offer shape (see SupplierOfferPayload).
// Parsing is all this adapter owns; validation + upsert live in SourceOfferImporter.
public sealed class JsonApiConnector(HttpClient http, SourceOfferImporter importer) : IConnector
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

        var raws = items.Select(ToRawOffer).ToList();
        return await importer.ImportAsync(connection, raws, DateTime.UtcNow, ct);
    }

    // Maps one JSON element to a RawOffer, preserving its exact text as the payload.
    // A shape mismatch yields an empty-keyed RawOffer so it still counts as fetched
    // and is skipped by the importer's validation.
    private static RawOffer ToRawOffer(JsonElement element)
    {
        var raw = element.GetRawText();
        SupplierOfferPayload? p;
        try { p = element.Deserialize<SupplierOfferPayload>(JsonOptions); }
        catch (JsonException) { p = null; }

        if (p is null)
            return new RawOffer("", "", null, "", null, 0, null, null, null, 0, null, null, raw);

        return new RawOffer(
            p.ExternalId ?? "", p.Title ?? "", p.Description, p.Country ?? "", p.City,
            p.Price, p.Currency, p.Board, p.Transport, p.Nights, p.ImageUrl, p.Tags, raw);
    }

    private static SupplierConnectionConfig? ReadConfig(string configJson)
    {
        try { return JsonSerializer.Deserialize<SupplierConnectionConfig>(configJson, JsonOptions); }
        catch (JsonException) { return null; }
    }

    private sealed record SupplierConnectionConfig(string? Url);

    // Canonical supplier offer shape (integration contract v1).
    private sealed record SupplierOfferPayload(
        string? ExternalId,
        string? Title,
        string? Description,
        string? Country,
        string? City,
        decimal Price,
        string? Currency,
        string? Board,
        string? Transport,
        int Nights,
        string? ImageUrl,
        List<string>? Tags);
}
