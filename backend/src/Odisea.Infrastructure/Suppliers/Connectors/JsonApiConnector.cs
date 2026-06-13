using System.Globalization;
using System.Text.Json;
using Odisea.Application.Suppliers.Connectors;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;

namespace Odisea.Infrastructure.Suppliers.Connectors;

// Pulls offers from a supplier's JSON HTTP endpoint. The endpoint must return a
// JSON array of objects; each object's fields are read by their canonical name,
// or by the supplier's name when the connection config supplies a fieldMap.
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
        var config = ConnectorConfig.Parse(connection.ConfigJson);
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

        var raws = items.Select(e => ToRawOffer(e, config)).ToList();
        return await importer.ImportAsync(connection, raws, DateTime.UtcNow, ct);
    }

    // Reads one JSON object into a RawOffer using the field map, preserving its
    // exact text as the payload. A non-object element yields an empty-keyed
    // RawOffer so it still counts as fetched and is skipped by the importer.
    private static RawOffer ToRawOffer(JsonElement element, ConnectorConfig config)
    {
        var raw = element.GetRawText();
        if (element.ValueKind != JsonValueKind.Object)
            return new RawOffer("", "", null, "", null, 0, null, null, null, 0, null, null, raw);

        return new RawOffer(
            Str(element, config.Field("externalId")) ?? "",
            Str(element, config.Field("title")) ?? "",
            Str(element, config.Field("description")),
            Str(element, config.Field("country")) ?? "",
            Str(element, config.Field("city")),
            Dec(element, config.Field("price")),
            Str(element, config.Field("currency")),
            Str(element, config.Field("board")),
            Str(element, config.Field("transport")),
            Int(element, config.Field("nights")),
            Str(element, config.Field("imageUrl")),
            Tags(element, config.Field("tags")),
            raw);
    }

    private static JsonElement? Prop(JsonElement obj, string name)
    {
        foreach (var p in obj.EnumerateObject())
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                return p.Value;
        return null;
    }

    private static string? Str(JsonElement obj, string name) =>
        Prop(obj, name) is { } v
            ? v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.GetRawText(),
                _ => null,
            }
            : null;

    private static decimal Dec(JsonElement obj, string name)
    {
        if (Prop(obj, name) is not { } v) return 0m;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
        if (v.ValueKind == JsonValueKind.String &&
            decimal.TryParse(v.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var ds)) return ds;
        return 0m;
    }

    private static int Int(JsonElement obj, string name)
    {
        if (Prop(obj, name) is not { } v) return 0;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i;
        if (v.ValueKind == JsonValueKind.String &&
            int.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s)) return s;
        return 0;
    }

    private static IReadOnlyList<string>? Tags(JsonElement obj, string name)
    {
        if (Prop(obj, name) is not { ValueKind: JsonValueKind.Array } v) return null;
        return v.EnumerateArray()
            .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : "")
            .Where(t => t.Length > 0)
            .ToList();
    }
}
