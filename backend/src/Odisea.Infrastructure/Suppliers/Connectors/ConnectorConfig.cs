using System.Text.Json;
using System.Text.Json.Serialization;

namespace Odisea.Infrastructure.Suppliers.Connectors;

// Parsed view of a SupplierConnection.ConfigJson, shared by the fetch-based
// adapters. `fieldMap` lets a supplier whose feed uses non-canonical names map
// them onto the canonical fields, e.g. {"externalId":"id","price":"cost"}; an
// unmapped canonical field falls back to its own name.
public sealed class ConnectorConfig
{
    public string? Url { get; init; }

    [JsonPropertyName("fieldMap")]
    public Dictionary<string, string>? FieldMap { get; init; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // The supplier's field/element name for a canonical field, or the canonical
    // name itself when there's no override.
    public string Field(string canonical) =>
        FieldMap is not null && FieldMap.TryGetValue(canonical, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : canonical;

    public static ConnectorConfig? Parse(string configJson)
    {
        try { return JsonSerializer.Deserialize<ConnectorConfig>(configJson, JsonOptions); }
        catch (JsonException) { return null; }
    }
}
