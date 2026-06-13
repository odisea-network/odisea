using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using Odisea.Application.Suppliers.Connectors;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;

namespace Odisea.Infrastructure.Suppliers.Connectors;

// Pulls offers from a supplier's XML HTTP feed. The feed must be a document of
// <offer> elements (at any depth) in the canonical shape:
//
//   <offer>
//     <externalId>…</externalId> <title>…</title> <country>…</country>
//     <price>540</price> <board>HalfBoard</board> <transport>Plane</transport>
//     <nights>7</nights> <tags><tag>beach</tag></tags> …
//   </offer>
//
// Element names are matched case-insensitively. Like the JSON adapter this only
// owns fetch + parse; validation + upsert live in SourceOfferImporter.
public sealed class XmlConnector(HttpClient http, SourceOfferImporter importer) : IConnector
{
    public SupplierConnectionKind Kind => SupplierConnectionKind.Xml;

    public async Task<ConnectorRunResult> RunAsync(SupplierConnection connection, CancellationToken ct)
    {
        var url = ReadUrl(connection.ConfigJson);
        if (string.IsNullOrWhiteSpace(url))
            return ConnectorRunResult.Empty("Connection config is missing a 'url'.");

        XDocument doc;
        try
        {
            var body = await http.GetStringAsync(url, ct);
            doc = XDocument.Parse(body);
        }
        catch (Exception ex)
        {
            // A failed fetch/parse is a failed run, not an exception — ImportRunner
            // records it and leaves the last good freshness intact.
            return ConnectorRunResult.Empty($"Fetch failed: {ex.Message}");
        }

        var raws = doc.Descendants()
            .Where(e => string.Equals(e.Name.LocalName, "offer", StringComparison.OrdinalIgnoreCase))
            .Select(ToRawOffer)
            .ToList();

        return await importer.ImportAsync(connection, raws, DateTime.UtcNow, ct);
    }

    private static RawOffer ToRawOffer(XElement offer) => new(
        Child(offer, "externalId") ?? "",
        Child(offer, "title") ?? "",
        Child(offer, "description"),
        Child(offer, "country") ?? "",
        Child(offer, "city"),
        ParseDecimal(Child(offer, "price")),
        Child(offer, "currency"),
        Child(offer, "board"),
        Child(offer, "transport"),
        ParseInt(Child(offer, "nights")),
        Child(offer, "imageUrl"),
        Tags(offer),
        offer.ToString());

    private static string? Child(XElement parent, string name) =>
        parent.Elements()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))
            ?.Value.Trim();

    private static IReadOnlyList<string>? Tags(XElement offer)
    {
        var container = offer.Elements()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, "tags", StringComparison.OrdinalIgnoreCase));
        if (container is null) return null;

        return container.Elements()
            .Select(t => t.Value.Trim())
            .Where(v => v.Length > 0)
            .ToList();
    }

    private static decimal ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;

    private static int ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : 0;

    // ConfigJson is JSON regardless of connector kind; the XML feed's URL lives there.
    private static string? ReadUrl(string configJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(configJson);
            return doc.RootElement.TryGetProperty("url", out var url) ? url.GetString() : null;
        }
        catch (JsonException) { return null; }
    }
}
