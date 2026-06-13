using System.Globalization;
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
        var config = ConnectorConfig.Parse(connection.ConfigJson);
        if (config is null || string.IsNullOrWhiteSpace(config.Url))
            return ConnectorRunResult.Empty("Connection config is missing a 'url'.");

        XDocument doc;
        try
        {
            var body = await http.GetStringAsync(config.Url, ct);
            doc = XDocument.Parse(body);
        }
        catch (Exception ex)
        {
            // A failed fetch/parse is a failed run, not an exception — ImportRunner
            // records it and leaves the last good freshness intact.
            return ConnectorRunResult.Empty($"Fetch failed: {ex.Message}");
        }

        // An <offer> element is matched by the mapped name too, so a feed whose row
        // element isn't literally "offer" still works.
        var offerName = config.Field("offer");
        var raws = doc.Descendants()
            .Where(e => string.Equals(e.Name.LocalName, offerName, StringComparison.OrdinalIgnoreCase))
            .Select(e => ToRawOffer(e, config))
            .ToList();

        return await importer.ImportAsync(connection, raws, DateTime.UtcNow, ct);
    }

    private static RawOffer ToRawOffer(XElement offer, ConnectorConfig config) => new(
        Child(offer, config.Field("externalId")) ?? "",
        Child(offer, config.Field("title")) ?? "",
        Child(offer, config.Field("description")),
        Child(offer, config.Field("country")) ?? "",
        Child(offer, config.Field("city")),
        ParseDecimal(Child(offer, config.Field("price"))),
        Child(offer, config.Field("currency")),
        Child(offer, config.Field("board")),
        Child(offer, config.Field("transport")),
        ParseInt(Child(offer, config.Field("nights"))),
        Child(offer, config.Field("imageUrl")),
        Tags(offer, config.Field("tags")),
        offer.ToString());

    private static string? Child(XElement parent, string name) =>
        parent.Elements()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))
            ?.Value.Trim();

    private static IReadOnlyList<string>? Tags(XElement offer, string name)
    {
        var container = offer.Elements()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));
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
}
