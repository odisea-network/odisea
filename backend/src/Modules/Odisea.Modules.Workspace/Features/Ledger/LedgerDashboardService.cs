using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Odisea.Modules.Workspace.Domain.Ledger;
using Odisea.Modules.Workspace.Infrastructure;
using Odisea.SharedKernel;

namespace Odisea.Modules.Workspace.Features.Ledger;

public sealed record LedgerCurrencyTotalsDto(
    string Currency,
    decimal Outstanding,
    decimal Overdue,
    decimal DueIn7Days,
    decimal DueIn30Days,
    decimal CommissionExpected,
    decimal CommissionReceived);

public sealed record LedgerDueItemDto(
    Guid ReservationId,
    string Reference,
    string TourOperatorName,
    string HolderName,
    LedgerReservationStatus ReservationStatus,
    LedgerInstallmentLabel Label,
    DateOnly DueDate,
    decimal RemainingAmount,
    string Currency,
    bool IsOverdue);

/// Counts are reservations; money is per currency and never added across currencies.
/// DueIn30Days includes DueIn7Days; neither includes what is already overdue.
public sealed record LedgerDashboardDto(
    DateOnly Today,
    int ActiveReservations,
    int OverdueReservations,
    int DueIn7DaysReservations,
    int DueIn30DaysReservations,
    List<LedgerCurrencyTotalsDto> Totals,
    List<LedgerDueItemDto> NeedsAction);

/// What the agency owes and when, at a glance. Read-only, so it stays available after a downgrade.
/// Drafts are left out: nothing is owed on a booking that is not confirmed. Cancelled reservations
/// stay in until someone corrects their figures, because cancelling does not settle anything.
public sealed class LedgerDashboardService(WorkspaceDbContext db, IClock clock, IOptions<LedgerOptions> options)
{
    private const int NeedsActionLimit = 20;

    public async Task<LedgerDashboardDto> GetAsync(Guid agencyId, CancellationToken ct)
    {
        var today = options.Value.Today(clock);
        var overdue = DueWindow.Overdue(today);
        var week = DueWindow.Upcoming(today, 7);
        var month = DueWindow.Upcoming(today, 30);

        var mine = db.LedgerReservations.AsNoTracking().Where(r => r.AgencyId == agencyId);
        var active = await mine.CountAsync(r => r.Status == LedgerReservationStatus.Confirmed, ct);

        // Only reservations that can still owe something are loaded; settled ones are the bulk over time.
        var open = await mine
            .Where(r => r.Status != LedgerReservationStatus.Draft)
            .Where(r =>
                (r.Payments.Where(p => p.VoidedAt == null).Sum(p => (decimal?)p.Amount) ?? 0) < r.OperatorTotal
                || (r.Payments.Where(p => p.VoidedAt == null).Sum(p => (decimal?)p.Amount) ?? 0)
                    < (r.Installments.Sum(i => (decimal?)i.Amount) ?? 0))
            .Include(r => r.Installments).Include(r => r.Payments)
            .AsSplitQuery()
            .ToListAsync(ct);

        var commissions = await mine
            .Where(r => (r.Status == LedgerReservationStatus.Confirmed || r.Status == LedgerReservationStatus.Completed)
                        && r.CommissionExpected != null)
            .GroupBy(r => r.Currency)
            .Select(g => new { Currency = g.Key, Expected = g.Sum(r => r.CommissionExpected ?? 0), Received = g.Sum(r => r.CommissionReceived) })
            .ToListAsync(ct);

        var money = new Dictionary<string, (decimal Outstanding, decimal Overdue, decimal Week, decimal Month)>();
        var items = new List<(LedgerReservation Reservation, LedgerInstallment Installment, decimal Remaining)>();
        int overdueCount = 0, weekCount = 0, monthCount = 0;

        foreach (var reservation in open)
        {
            var totals = LedgerCalculator.Compute(reservation, today);
            var unpaid = reservation.Installments
                .Select(i => (Installment: i, totals.Installments[i.Id].Remaining))
                .Where(x => x.Remaining > 0)
                .ToList();
            decimal Sum(DueWindow window) => unpaid.Where(x => window.Contains(x.Installment.DueDate)).Sum(x => x.Remaining);

            var (late, inWeek, inMonth) = (Sum(overdue), Sum(week), Sum(month));
            if (late > 0) overdueCount++;
            if (inWeek > 0) weekCount++;
            if (inMonth > 0) monthCount++;

            var sum = money.GetValueOrDefault(reservation.Currency);
            money[reservation.Currency] = (sum.Outstanding + totals.Outstanding, sum.Overdue + late, sum.Week + inWeek, sum.Month + inMonth);
            items.AddRange(unpaid
                .Where(x => x.Installment.DueDate <= month.To)
                .Select(x => (reservation, x.Installment, x.Remaining)));
        }

        var urgent = items
            .OrderBy(x => x.Installment.DueDate).ThenBy(x => x.Reservation.Reference).ThenBy(x => x.Installment.SortOrder)
            .Take(NeedsActionLimit)
            .ToList();
        var operatorIds = urgent.Select(x => x.Reservation.TourOperatorId).Distinct().ToList();
        var names = await db.TourOperators.AsNoTracking()
            .Where(t => t.AgencyId == agencyId && operatorIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        var currencies = money.Keys.Union(commissions.Select(c => c.Currency)).Order();
        return new LedgerDashboardDto(
            today, active, overdueCount, weekCount, monthCount,
            [.. currencies.Select(currency =>
            {
                var sum = money.GetValueOrDefault(currency);
                var commission = commissions.FirstOrDefault(c => c.Currency == currency);
                return new LedgerCurrencyTotalsDto(currency, sum.Outstanding, sum.Overdue, sum.Week, sum.Month,
                    commission?.Expected ?? 0, commission?.Received ?? 0);
            })],
            [.. urgent.Select(x => new LedgerDueItemDto(
                x.Reservation.Id, x.Reservation.Reference, names.GetValueOrDefault(x.Reservation.TourOperatorId, ""),
                x.Reservation.HolderName, x.Reservation.Status, x.Installment.Label, x.Installment.DueDate,
                x.Remaining, x.Reservation.Currency, overdue.Contains(x.Installment.DueDate)))]);
    }
}
