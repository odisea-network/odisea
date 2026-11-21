using Odisea.Modules.Workspace.Domain.Ledger;

namespace Odisea.Modules.Workspace.Features.Ledger;

public enum InstallmentPaymentStatus
{
    Unpaid,
    PartiallyPaid,
    Paid,
}

public enum CommissionStatus
{
    Expected,
    PartiallyReceived,
    Received,
}

public sealed record InstallmentState(decimal Paid, decimal Remaining, InstallmentPaymentStatus Status, bool IsOverdue);

public sealed record LedgerTotals(
    decimal ScheduledTotal,
    decimal PaidTotal,
    decimal Outstanding,
    decimal Overpaid,
    decimal ScheduleDifference,
    decimal OverdueAmount,
    DateOnly? NextDueDate,
    IReadOnlyDictionary<Guid, InstallmentState> Installments);

/// All ledger arithmetic in one place, free of EF and of the clock.
public static class LedgerCalculator
{
    public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    /// Payments are not tied to an instalment: whatever was paid covers the schedule in order, first
    /// instalment first. One transfer can therefore settle several instalments, and a part-payment
    /// leaves exactly one instalment partly paid.
    public static LedgerTotals Compute(
        decimal operatorTotal, IEnumerable<LedgerInstallment> installments, decimal paidTotal, DateOnly today)
    {
        var ordered = installments.OrderBy(i => i.SortOrder).ThenBy(i => i.DueDate).ToList();
        var states = new Dictionary<Guid, InstallmentState>(ordered.Count);
        var left = paidTotal;
        decimal overdue = 0;
        DateOnly? nextDue = null;

        foreach (var installment in ordered)
        {
            var paid = Math.Min(left, installment.Amount);
            left -= paid;
            var remaining = installment.Amount - paid;
            var isOverdue = remaining > 0 && installment.DueDate < today;
            if (isOverdue)
                overdue += remaining;
            if (remaining > 0 && (nextDue is null || installment.DueDate < nextDue))
                nextDue = installment.DueDate;

            states[installment.Id] = new InstallmentState(
                paid, remaining,
                remaining == 0 ? InstallmentPaymentStatus.Paid
                    : paid > 0 ? InstallmentPaymentStatus.PartiallyPaid
                    : InstallmentPaymentStatus.Unpaid,
                isOverdue);
        }

        var scheduled = ordered.Sum(i => i.Amount);
        return new LedgerTotals(
            scheduled,
            paidTotal,
            Math.Max(0, operatorTotal - paidTotal),
            Math.Max(0, paidTotal - operatorTotal),
            operatorTotal - scheduled,
            overdue,
            nextDue,
            states);
    }

    public static LedgerTotals Compute(LedgerReservation reservation, DateOnly today) =>
        Compute(reservation.OperatorTotal, reservation.Installments, reservation.PaidTotal, today);

    /// A percentage is taken from the chosen base; a fixed or manual commission is the typed amount.
    /// Null when the base amount is not known (percentage of a customer price that was not entered).
    public static decimal? ExpectedCommission(
        CommissionType type, decimal? rate, CommissionBase commissionBase,
        decimal operatorTotal, decimal? customerSaleAmount, decimal? manualAmount)
    {
        if (type == CommissionType.Fixed || commissionBase == CommissionBase.Manual)
            return manualAmount;
        var amount = commissionBase == CommissionBase.CustomerSale ? customerSaleAmount : operatorTotal;
        return amount is { } a && rate is { } r ? Round(a * r / 100m) : null;
    }

    public static CommissionStatus? StatusOf(decimal? expected, decimal received) =>
        expected is not { } e ? null
        : received > 0 && received >= e ? CommissionStatus.Received
        : received > 0 ? CommissionStatus.PartiallyReceived
        : CommissionStatus.Expected;
}
