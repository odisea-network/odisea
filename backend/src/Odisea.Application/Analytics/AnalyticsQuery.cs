using Microsoft.EntityFrameworkCore;
using Odisea.Application.Analytics.Dtos;
using Odisea.Application.Common.Interfaces;
using Odisea.Domain.Enums;

namespace Odisea.Application.Analytics;

public static class AnalyticsQuery
{
    /// <summary>
    /// Aggregates events for a publication within the given window into counts and
    /// conversion rates. Counts are computed in a single grouped DB round-trip.
    /// </summary>
    public static async Task<PublicationStatsDto> GetStatsAsync(
        string publicationKey,
        AnalyticsWindow window,
        IAppDbContext db,
        CancellationToken ct = default)
    {
        var to = DateTime.UtcNow;
        var from = to - window.ToTimeSpan();

        var counts = await db.Events
            .Where(e => e.PublicationKey == publicationKey
                && e.OccurredAt >= from
                && e.OccurredAt <= to)
            .GroupBy(e => e.EventType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int Count(EventType t) => counts.FirstOrDefault(c => c.Type == t)?.Count ?? 0;

        var impressions = Count(EventType.Impression);
        var opens = Count(EventType.Open);
        var inquiryStarts = Count(EventType.InquiryStart);
        var inquirySubmits = Count(EventType.InquirySubmit);

        // Open rate = opens / impressions; inquiry rate = submitted inquiries / opens.
        var openRate = impressions > 0 ? Math.Round((double)opens / impressions, 4) : 0d;
        var inquiryRate = opens > 0 ? Math.Round((double)inquirySubmits / opens, 4) : 0d;

        return new PublicationStatsDto(
            PublicationKey: publicationKey,
            Window: window.ToToken(),
            From: from,
            To: to,
            Impressions: impressions,
            Opens: opens,
            InquiryStarts: inquiryStarts,
            InquirySubmits: inquirySubmits,
            OpenRate: openRate,
            InquiryRate: inquiryRate);
    }
}
