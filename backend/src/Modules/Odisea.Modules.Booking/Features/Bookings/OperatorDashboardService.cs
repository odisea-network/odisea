using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Booking.Domain;
using Odisea.Modules.Booking.Infrastructure;
using Odisea.SharedKernel;

namespace Odisea.Modules.Booking.Features.Bookings;

/// The operator's landing page: what needs a decision now, and how the period is going.
public class OperatorDashboardService(BookingDbContext db, IClock clock)
{
    private const string BaseCurrency = "EUR";
    private static readonly TimeSpan DueSoonWindow = TimeSpan.FromHours(48);
    private static readonly TimeSpan FailedWindow = TimeSpan.FromHours(24);
    private const int QueueLimit = 20;

    public async Task<OperatorDashboardDto> BuildAsync(int days, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var since = now.AddDays(-days);
        var previousSince = now.AddDays(-2 * days);

        // Awaiting payment is the only status where money decides the next step; it is a small set.
        var awaiting = await db.Bookings
            .Include(b => b.Payments)
            .AsNoTracking()
            .Where(b => b.Status == BookingStatus.AwaitingPayment)
            .ToListAsync(ct);

        var ready = awaiting.Where(b => b.IsFullyPaid).OrderBy(b => b.PaymentDueAt).ToList();
        var unpaid = awaiting.Where(b => !b.IsFullyPaid).ToList();
        var overdue = unpaid.Where(b => b.PaymentDueAt < now).OrderBy(b => b.PaymentDueAt).ToList();
        var soon = unpaid.Where(b => b.PaymentDueAt >= now && b.PaymentDueAt <= now + DueSoonWindow)
            .OrderBy(b => b.PaymentDueAt).ToList();
        var partial = unpaid.Where(b => b.PaidAmount > 0 && !overdue.Contains(b) && !soon.Contains(b))
            .OrderBy(b => b.PaymentDueAt).ToList();

        var failedSince = now - FailedWindow;
        var failedStatus = BookingStatus.Failed.ToString();
        var failed = await db.Bookings
            .Include(b => b.History)
            .AsNoTracking()
            .Where(b => b.Status == BookingStatus.Failed
                && b.History.Any(h => h.ToStatus == failedStatus && h.CreatedAt >= failedSince))
            .ToListAsync(ct);

        var queue = new List<OperatorAttentionItem>();
        queue.AddRange(overdue.Select(b => Item(b, "overdue", "Unpaid past the deadline — the hold is still open", b.BalanceDue, b.PaymentDueAt)));
        queue.AddRange(failed
            .Select(b => (Booking: b, FailedAt: b.History.Where(h => h.ToStatus == failedStatus).Max(h => h.CreatedAt)))
            .OrderByDescending(x => x.FailedAt)
            .Select(x => Item(x.Booking, "failed",
                x.Booking.History.Where(h => h.ToStatus == failedStatus).OrderByDescending(h => h.CreatedAt).First().Note ?? "Supplier hold failed",
                x.Booking.SellAmount ?? 0m, x.FailedAt)));
        queue.AddRange(ready.Select(b => Item(b, "ready", $"Paid in full · {b.PaidAmount:0.00} {b.Currency} received", b.SellAmount ?? 0m, b.PaymentDueAt)));
        queue.AddRange(soon.Select(b => Item(b, "soon", PaidText(b), b.BalanceDue, b.PaymentDueAt)));
        queue.AddRange(partial.Select(b => Item(b, "partial", PaidText(b), b.BalanceDue, b.PaymentDueAt)));

        var created = await db.Bookings.CountAsync(b => b.CreatedAt >= since, ct);
        var createdPrevious = await db.Bookings.CountAsync(b => b.CreatedAt >= previousSince && b.CreatedAt < since, ct);
        var createdViaApi = await db.Bookings.CountAsync(
            b => b.CreatedAt >= since && b.History.Any(h => h.ActorType == BookingActorType.ApiClient), ct);

        var confirmedStatus = BookingStatus.Confirmed.ToString();
        var confirmed = await db.Bookings
            .AsNoTracking()
            .Where(b => b.Status == BookingStatus.Confirmed
                && b.History.Any(h => h.ToStatus == confirmedStatus && h.CreatedAt >= since))
            .Select(b => new { b.AgencyId, b.SellAmount, b.NetAmount })
            .ToListAsync(ct);

        var topAgencies = confirmed
            .GroupBy(b => b.AgencyId)
            .Select(g => new OperatorAgencyFigure(g.Key, g.Count(), g.Sum(b => b.SellAmount ?? 0m), BaseCurrency))
            .OrderByDescending(a => a.Sell)
            .ThenByDescending(a => a.Bookings)
            .Take(5)
            .ToList();

        return new OperatorDashboardDto(
            now, days,
            Kpi(ready, b => b.SellAmount ?? 0m),
            Kpi(soon, b => b.BalanceDue),
            Kpi(overdue, b => b.BalanceDue),
            new OperatorDashboardKpi(failed.Count, failed.Sum(b => b.SellAmount ?? 0m), BaseCurrency),
            queue.Take(QueueLimit).ToList(),
            created, createdPrevious, createdViaApi,
            confirmed.Sum(b => b.SellAmount ?? 0m),
            confirmed.Sum(b => (b.SellAmount ?? 0m) - (b.NetAmount ?? 0m)),
            BaseCurrency,
            topAgencies);
    }

    private static OperatorDashboardKpi Kpi(List<Domain.Booking> bookings, Func<Domain.Booking, decimal> amount) =>
        new(bookings.Count, bookings.Sum(amount), BaseCurrency);

    private static OperatorAttentionItem Item(Domain.Booking b, string kind, string text, decimal amount, DateTimeOffset? at) =>
        new(b.Id, b.Ref, b.AgencyId, kind, text, amount, b.Currency, at);

    private static string PaidText(Domain.Booking b) =>
        b.PaidAmount > 0
            ? $"{b.PaidAmount:0.00} of {b.SellAmount ?? 0m:0.00} {b.Currency} paid"
            : "Nothing paid yet";
}
