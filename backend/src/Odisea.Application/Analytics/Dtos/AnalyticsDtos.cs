using Odisea.Domain.Enums;

namespace Odisea.Application.Analytics.Dtos;

/// <summary>
/// A single inbound analytics event. Enum-shaped fields arrive as strings from
/// the embed runtime and are parsed leniently (case-insensitive) on ingest.
/// </summary>
public record EventDto(
    string EventType,
    string PublicationKey,
    Guid? OfferId,
    string? Channel,
    DateTime? OccurredAt
);

/// <summary>Batch ingest payload — one or many events in a single POST.</summary>
public record IngestEventCommand(IReadOnlyList<EventDto> Events);

/// <summary>The window an analytics query is scoped to.</summary>
public enum AnalyticsWindow { Last24h, Last7d, Last30d }

/// <summary>Per-publication aggregates for a single time window.</summary>
public record PublicationStatsDto(
    string PublicationKey,
    string Window,
    DateTime From,
    DateTime To,
    int Impressions,
    int Opens,
    int InquiryStarts,
    int InquirySubmits,
    double OpenRate,
    double InquiryRate
);

public static class AnalyticsWindowExtensions
{
    /// <summary>Parses the public window token (24h|7d|30d). Returns false for anything else.</summary>
    public static bool TryParse(string? value, out AnalyticsWindow window)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "24h": window = AnalyticsWindow.Last24h; return true;
            case "7d":  window = AnalyticsWindow.Last7d;  return true;
            case "30d": window = AnalyticsWindow.Last30d; return true;
            default:    window = AnalyticsWindow.Last7d;  return false;
        }
    }

    public static TimeSpan ToTimeSpan(this AnalyticsWindow window) => window switch
    {
        AnalyticsWindow.Last24h => TimeSpan.FromHours(24),
        AnalyticsWindow.Last7d  => TimeSpan.FromDays(7),
        AnalyticsWindow.Last30d => TimeSpan.FromDays(30),
        _ => TimeSpan.FromDays(7),
    };

    public static string ToToken(this AnalyticsWindow window) => window switch
    {
        AnalyticsWindow.Last24h => "24h",
        AnalyticsWindow.Last7d  => "7d",
        AnalyticsWindow.Last30d => "30d",
        _ => "7d",
    };
}
