using Microsoft.EntityFrameworkCore;
using Odisea.Application.Analytics;
using Odisea.Application.Analytics.Dtos;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Infrastructure.Data;

namespace Odisea.UnitTests.Analytics;

public class AnalyticsQueryTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static Event Ev(string key, EventType type, DateTime occurredAt) => new()
    {
        EventType = type,
        PublicationKey = key,
        Channel = Channel.WebComponent,
        OccurredAt = occurredAt,
    };

    [Fact]
    public async Task GetStats_CountsAndRates_AreCorrectWithinWindow()
    {
        await using var db = CreateDb();
        var now = DateTime.UtcNow;

        db.Events.AddRange(
            // 10 impressions, 4 opens, 2 inquiry-starts, 1 inquiry-submit — all within 24h
            Enumerable.Range(0, 10).Select(i => Ev("pubA", EventType.Impression, now.AddMinutes(-i))).ToArray());
        db.Events.AddRange(
            Enumerable.Range(0, 4).Select(i => Ev("pubA", EventType.Open, now.AddMinutes(-i))).ToArray());
        db.Events.AddRange(
            Enumerable.Range(0, 2).Select(i => Ev("pubA", EventType.InquiryStart, now.AddMinutes(-i))).ToArray());
        db.Events.Add(Ev("pubA", EventType.InquirySubmit, now.AddMinutes(-1)));
        await db.SaveChangesAsync();

        var stats = await AnalyticsQuery.GetStatsAsync("pubA", AnalyticsWindow.Last24h, db);

        Assert.Equal(10, stats.Impressions);
        Assert.Equal(4, stats.Opens);
        Assert.Equal(2, stats.InquiryStarts);
        Assert.Equal(1, stats.InquirySubmits);
        Assert.Equal(0.4, stats.OpenRate);     // 4 / 10
        Assert.Equal(0.25, stats.InquiryRate); // 1 / 4
        Assert.Equal("24h", stats.Window);
    }

    [Fact]
    public async Task GetStats_ExcludesEventsOutsideWindow()
    {
        await using var db = CreateDb();
        var now = DateTime.UtcNow;

        db.Events.Add(Ev("pubB", EventType.Impression, now.AddHours(-1)));   // in 24h
        db.Events.Add(Ev("pubB", EventType.Impression, now.AddDays(-3)));    // out of 24h, in 7d
        await db.SaveChangesAsync();

        var last24h = await AnalyticsQuery.GetStatsAsync("pubB", AnalyticsWindow.Last24h, db);
        var last7d = await AnalyticsQuery.GetStatsAsync("pubB", AnalyticsWindow.Last7d, db);

        Assert.Equal(1, last24h.Impressions);
        Assert.Equal(2, last7d.Impressions);
    }

    [Fact]
    public async Task GetStats_ScopesToRequestedPublicationOnly()
    {
        await using var db = CreateDb();
        var now = DateTime.UtcNow;

        db.Events.Add(Ev("pubC", EventType.Impression, now));
        db.Events.Add(Ev("pubD", EventType.Impression, now));
        await db.SaveChangesAsync();

        var stats = await AnalyticsQuery.GetStatsAsync("pubC", AnalyticsWindow.Last7d, db);

        Assert.Equal(1, stats.Impressions);
    }

    [Fact]
    public async Task GetStats_NoEvents_ReturnsZeroesAndZeroRates()
    {
        await using var db = CreateDb();

        var stats = await AnalyticsQuery.GetStatsAsync("empty", AnalyticsWindow.Last30d, db);

        Assert.Equal(0, stats.Impressions);
        Assert.Equal(0, stats.Opens);
        Assert.Equal(0d, stats.OpenRate);
        Assert.Equal(0d, stats.InquiryRate);
    }
}
