using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Workspace.Domain;
using Odisea.Modules.Workspace.Infrastructure;
using Odisea.SharedKernel;

namespace Odisea.Modules.Workspace.Features.Usage;

/// Operator oversight: who used how much of the paid tier in a month (billing input).
public sealed record WorkspaceUsageDto(Guid AgencyId, string Month, int ExternalReservations, int InquirySubmissions);

public sealed class WorkspaceUsageService(WorkspaceDbContext db, IClock clock)
{
    public async Task<List<WorkspaceUsageDto>> UsageAsync(string? month, CancellationToken ct)
    {
        var (start, end, label) = MonthRange(month);
        var reservations = await db.ExternalReservations.AsNoTracking()
            .Where(r => r.CreatedAt >= start && r.CreatedAt < end)
            .GroupBy(r => r.AgencyId)
            .Select(g => new { AgencyId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var inquiries = await db.InquirySubmissions.AsNoTracking()
            .Where(s => s.CreatedAt >= start && s.CreatedAt < end)
            .GroupBy(s => s.AgencyId)
            .Select(g => new { AgencyId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var byAgency = reservations.ToDictionary(r => r.AgencyId, r => (Reservations: r.Count, Inquiries: 0));
        foreach (var i in inquiries)
            byAgency[i.AgencyId] = byAgency.TryGetValue(i.AgencyId, out var current)
                ? (current.Reservations, i.Count)
                : (0, i.Count);

        return byAgency
            .Select(kv => new WorkspaceUsageDto(kv.Key, label, kv.Value.Reservations, kv.Value.Inquiries))
            .OrderByDescending(u => u.ExternalReservations + u.InquirySubmissions)
            .ToList();
    }

    private (DateTimeOffset Start, DateTimeOffset End, string Label) MonthRange(string? month)
    {
        DateTimeOffset start;
        if (string.IsNullOrWhiteSpace(month))
        {
            var now = clock.UtcNow;
            start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        }
        else if (DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            start = new DateTimeOffset(parsed.Year, parsed.Month, 1, 0, 0, 0, TimeSpan.Zero);
        }
        else
        {
            throw new WorkspaceValidationException("month must be yyyy-MM.");
        }
        return (start, start.AddMonths(1), start.ToString("yyyy-MM", CultureInfo.InvariantCulture));
    }
}
