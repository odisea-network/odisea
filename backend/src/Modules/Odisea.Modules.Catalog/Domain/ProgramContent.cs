namespace Odisea.Modules.Catalog.Domain;

/// What the customer reads before booking. Owned by Program (same table). Long texts are
/// Markdown; the lists are short plain-text bullets. Nothing here affects pricing or booking.
public class ProgramContent
{
    public const int MaxHighlights = 12;
    public const int MaxListItems = 40;
    public const int MaxTags = 15;

    /// One line under the title, e.g. "7 nights all-inclusive on the Turkish Riviera".
    public string? Tagline { get; set; }

    /// Two or three sentences for cards, previews and search snippets.
    public string? Summary { get; set; }

    public string[] Highlights { get; set; } = [];
    public string[] Inclusions { get; set; } = [];
    public string[] Exclusions { get; set; } = [];

    /// Lowercase kebab-case labels agencies filter by, e.g. "family", "all-inclusive".
    public string[] Tags { get; set; } = [];

    public TransportMode TransportMode { get; set; } = TransportMode.Unspecified;

    /// Carrier, route, baggage, times — whatever the customer needs to know about getting there.
    public string? TransportDetails { get; set; }

    /// Where the trip starts, e.g. "Sofia Airport (SOF)".
    public string? DeparturePoint { get; set; }

    public int? MinGroupSize { get; set; }
    public int? MaxGroupSize { get; set; }

    /// Documents and visas, currency, climate, what to pack, local customs.
    public string? PracticalInfo { get; set; }

    public string? CancellationPolicy { get; set; }
    public string? PaymentTerms { get; set; }
    public string? InsurancePolicy { get; set; }
    public string? ChildPolicy { get; set; }
}

public enum TransportMode
{
    Unspecified,
    Flight,
    Bus,
    Train,
    Ship,
    OwnTransport,
    Mixed,
}
