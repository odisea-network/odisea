namespace Odisea.Modules.Catalog.Domain;

/// What a program needs before agencies see it. Required checks block publishing; recommended
/// ones are what a well-made product page has, reported so the operator (or the agent writing the
/// program) can finish the job. Pure: everything it needs is passed in.
public static class ProgramReadiness
{
    public const int MinSummaryLength = 50;
    public const int MinDescriptionLength = 300;
    public const int MinHighlights = 3;
    public const int MinGalleryImages = 3;
    public const int MinCoverWidth = 1200;
    public const int MaxTitleLength = 70;
    public const int MinTitleLength = 10;

    public static ReadinessReport Evaluate(
        Program program, IReadOnlyList<ProgramImageInfo> images, int mappedHotels, DateOnly today)
    {
        var c = program.Content;
        var upcoming = program.Departures.Count(d => d.StartDate >= today);
        var cover = images.FirstOrDefault(i => i.IsCover);
        var withoutAlt = images.Count(i => !i.HasAltText);
        var withoutTransport = program.Departures.Count(d => string.IsNullOrWhiteSpace(d.TransportNote));
        var programDays = program.Departures.OrderBy(d => d.StartDate).Select(d => d.Period.Nights + 1).FirstOrDefault();
        var itineraryDays = program.Itinerary.Count;

        List<ReadinessCheck> checks =
        [
            Required("departures", "At least one upcoming departure",
                upcoming > 0 ? $"{upcoming} upcoming of {program.Departures.Count}" : "None yet",
                upcoming > 0),
            Required("hotels", "At least one allowed hotel",
                $"{program.Hotels.Count} allowed", program.Hotels.Count > 0),
            Required("summary", "Summary",
                $"{Length(c.Summary)} characters (at least {MinSummaryLength})",
                Length(c.Summary) >= MinSummaryLength),
            Required("cover", "Cover image",
                cover is null ? "No cover chosen" : $"{cover.Width}×{cover.Height}", cover is not null),
            Required("inclusions", "What's included",
                $"{c.Inclusions.Length} items", c.Inclusions.Length > 0),
            Required("cancellation", "Cancellation policy",
                Present(c.CancellationPolicy), !string.IsNullOrWhiteSpace(c.CancellationPolicy)),

            Recommended("title", "Title length",
                $"{program.Name.Length} characters ({MinTitleLength}–{MaxTitleLength})",
                program.Name.Length is >= MinTitleLength and <= MaxTitleLength),
            Recommended("tagline", "Tagline", Present(c.Tagline), !string.IsNullOrWhiteSpace(c.Tagline)),
            Recommended("description", "Full description",
                $"{Length(program.Description)} characters (at least {MinDescriptionLength})",
                Length(program.Description) >= MinDescriptionLength),
            Recommended("highlights", "Highlights",
                $"{c.Highlights.Length} (at least {MinHighlights})", c.Highlights.Length >= MinHighlights),
            Recommended("exclusions", "What's not included",
                $"{c.Exclusions.Length} items", c.Exclusions.Length > 0),
            Recommended("itinerary", "Day-by-day itinerary",
                programDays > 0
                    ? $"{itineraryDays} days described for a {programDays}-day program"
                    : $"{itineraryDays} days described",
                programDays > 0 ? itineraryDays == programDays : itineraryDays > 0),
            Recommended("transport", "Transport",
                c.TransportMode == TransportMode.Unspecified
                    ? "Mode not set"
                    : $"{c.TransportMode}, details {(string.IsNullOrWhiteSpace(c.TransportDetails) ? "missing" : "given")}",
                c.TransportMode != TransportMode.Unspecified && !string.IsNullOrWhiteSpace(c.TransportDetails)),
            Recommended("departure-point", "Departure point",
                Present(c.DeparturePoint), !string.IsNullOrWhiteSpace(c.DeparturePoint)),
            Recommended("transport-notes", "Transport note on every departure",
                withoutTransport == 0 ? "All departures have one" : $"{withoutTransport} without",
                withoutTransport == 0),
            Recommended("mappings", "Hotels mapped to the supplier",
                $"{mappedHotels} of {program.Hotels.Count} mapped",
                program.Hotels.Count > 0 && mappedHotels == program.Hotels.Count),
            Recommended("practical", "Practical information",
                Present(c.PracticalInfo), !string.IsNullOrWhiteSpace(c.PracticalInfo)),
            Recommended("payment", "Payment terms", Present(c.PaymentTerms), !string.IsNullOrWhiteSpace(c.PaymentTerms)),
            Recommended("insurance", "Insurance policy",
                Present(c.InsurancePolicy), !string.IsNullOrWhiteSpace(c.InsurancePolicy)),
            Recommended("children", "Child policy", Present(c.ChildPolicy), !string.IsNullOrWhiteSpace(c.ChildPolicy)),
            Recommended("gallery", "Gallery",
                $"{images.Count} images (at least {MinGalleryImages})", images.Count >= MinGalleryImages),
            Recommended("alt-text", "Alt text on every image",
                images.Count == 0 ? "No images" : withoutAlt == 0 ? "All images have it" : $"{withoutAlt} without",
                images.Count > 0 && withoutAlt == 0),
            Recommended("cover-size", "Cover at least " + MinCoverWidth + " px wide",
                cover is null ? "No cover" : $"{cover.Width} px", cover is { Width: >= MinCoverWidth }),
            Recommended("tags", "Tags", $"{c.Tags.Length} tags", c.Tags.Length > 0),
        ];

        return new ReadinessReport(checks);
    }

    private static ReadinessCheck Required(string code, string title, string detail, bool passed) =>
        new(code, title, detail, Required: true, passed);

    private static ReadinessCheck Recommended(string code, string title, string detail, bool passed) =>
        new(code, title, detail, Required: false, passed);

    private static int Length(string? text) => text?.Trim().Length ?? 0;

    private static string Present(string? text) => string.IsNullOrWhiteSpace(text) ? "Missing" : "Given";
}

public sealed record ReadinessCheck(string Code, string Title, string Detail, bool Required, bool Passed);

public sealed record ReadinessReport(IReadOnlyList<ReadinessCheck> Checks)
{
    public IEnumerable<ReadinessCheck> Blocking => Checks.Where(c => c.Required && !c.Passed);
    public bool CanPublish => !Blocking.Any();

    /// Share of all checks (required and recommended) that pass, 0–100.
    public int Score => Checks.Count == 0 ? 0 : (int)Math.Round(100.0 * Checks.Count(c => c.Passed) / Checks.Count);
}

/// What readiness needs to know about an image; the bytes stay in the database.
public sealed record ProgramImageInfo(Guid Id, bool IsCover, int Width, int Height, bool HasAltText);
