using Odisea.Modules.Workspace.Domain.Ledger;
using Odisea.Modules.Workspace.Features.Ledger;
using Xunit;

namespace Odisea.UnitTests.Workspace;

public class LedgerCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 12, 10);
    private static readonly Guid Reservation = Guid.NewGuid();

    private static LedgerInstallment Installment(decimal amount, DateOnly due, int order) => new()
    {
        ReservationId = Reservation,
        AgencyId = Guid.Empty,
        Amount = amount,
        DueDate = due,
        SortOrder = order,
    };

    /// The worked example from the product brief: 2000 EUR, 30 % deposit, 400 paid so far.
    [Fact]
    public void A_part_paid_deposit_leaves_the_rest_of_the_deposit_and_the_whole_balance_owed()
    {
        var deposit = Installment(600m, new DateOnly(2026, 12, 1), 0);
        var balance = Installment(1400m, new DateOnly(2027, 1, 15), 1);

        var totals = LedgerCalculator.Compute(2000m, [balance, deposit], paidTotal: 400m, Today);

        Assert.Equal(2000m, totals.ScheduledTotal);
        Assert.Equal(0m, totals.ScheduleDifference);
        Assert.Equal(400m, totals.PaidTotal);
        Assert.Equal(1600m, totals.Outstanding);
        Assert.Equal(0m, totals.Overpaid);
        Assert.Equal(new InstallmentState(400m, 200m, InstallmentPaymentStatus.PartiallyPaid, IsOverdue: true),
            totals.Installments[deposit.Id]);
        Assert.Equal(new InstallmentState(0m, 1400m, InstallmentPaymentStatus.Unpaid, IsOverdue: false),
            totals.Installments[balance.Id]);
        Assert.Equal(200m, totals.OverdueAmount);
        Assert.Equal(new DateOnly(2026, 12, 1), totals.NextDueDate);
    }

    [Fact]
    public void One_payment_can_settle_several_instalments_in_schedule_order()
    {
        var first = Installment(300m, new DateOnly(2026, 11, 1), 0);
        var second = Installment(300m, new DateOnly(2026, 11, 20), 1);
        var third = Installment(400m, new DateOnly(2027, 2, 1), 2);

        var totals = LedgerCalculator.Compute(1000m, [first, second, third], paidTotal: 650m, Today);

        Assert.Equal(InstallmentPaymentStatus.Paid, totals.Installments[first.Id].Status);
        Assert.Equal(InstallmentPaymentStatus.Paid, totals.Installments[second.Id].Status);
        Assert.Equal(new InstallmentState(50m, 350m, InstallmentPaymentStatus.PartiallyPaid, IsOverdue: false),
            totals.Installments[third.Id]);
        Assert.Equal(0m, totals.OverdueAmount);
        Assert.Equal(new DateOnly(2027, 2, 1), totals.NextDueDate);
    }

    [Fact]
    public void An_instalment_due_today_is_not_overdue_yet()
    {
        var dueToday = Installment(500m, Today, 0);

        Assert.False(LedgerCalculator.Compute(500m, [dueToday], 0m, Today).Installments[dueToday.Id].IsOverdue);
        Assert.True(LedgerCalculator.Compute(500m, [dueToday], 0m, Today.AddDays(1)).Installments[dueToday.Id].IsOverdue);
    }

    [Fact]
    public void No_schedule_still_reports_what_is_owed_and_flags_the_missing_plan()
    {
        var totals = LedgerCalculator.Compute(900m, [], paidTotal: 100m, Today);

        Assert.Equal(800m, totals.Outstanding);
        Assert.Equal(900m, totals.ScheduleDifference);
        Assert.Null(totals.NextDueDate);
        Assert.Empty(totals.Installments);
    }

    [Fact]
    public void Overpayment_is_reported_rather_than_hidden()
    {
        var only = Installment(500m, new DateOnly(2026, 12, 1), 0);

        var totals = LedgerCalculator.Compute(500m, [only], paidTotal: 620m, Today);

        Assert.Equal(0m, totals.Outstanding);
        Assert.Equal(120m, totals.Overpaid);
        Assert.Equal(InstallmentPaymentStatus.Paid, totals.Installments[only.Id].Status);
        Assert.Null(totals.NextDueDate);
    }

    [Fact]
    public void A_schedule_that_plans_more_or_less_than_the_price_shows_the_difference()
    {
        Assert.Equal(-100m, LedgerCalculator.Compute(1000m, [Installment(1100m, Today, 0)], 0m, Today).ScheduleDifference);
        Assert.Equal(250m, LedgerCalculator.Compute(1000m, [Installment(750m, Today, 0)], 0m, Today).ScheduleDifference);
    }

    [Fact]
    public void Percentage_commission_is_taken_from_the_chosen_base_and_rounded_to_cents()
    {
        Assert.Equal(140m, LedgerCalculator.ExpectedCommission(
            CommissionType.Percent, 7m, CommissionBase.OperatorTotal, 2000m, 2300m, null));
        Assert.Equal(161m, LedgerCalculator.ExpectedCommission(
            CommissionType.Percent, 7m, CommissionBase.CustomerSale, 2000m, 2300m, null));
        // 33.30 × 0.5 % = 0.1665: half a cent rounds away from zero.
        Assert.Equal(0.17m, LedgerCalculator.ExpectedCommission(
            CommissionType.Percent, 0.5m, CommissionBase.OperatorTotal, 33.30m, null, null));
        Assert.Null(LedgerCalculator.ExpectedCommission(
            CommissionType.Percent, 7m, CommissionBase.CustomerSale, 2000m, null, null));
    }

    [Fact]
    public void Fixed_and_manual_commission_are_the_typed_amount()
    {
        Assert.Equal(75m, LedgerCalculator.ExpectedCommission(CommissionType.Fixed, null, CommissionBase.Manual, 2000m, null, 75m));
        Assert.Equal(90m, LedgerCalculator.ExpectedCommission(CommissionType.Percent, 5m, CommissionBase.Manual, 2000m, null, 90m));
    }

    [Fact]
    public void Commission_stays_expected_until_something_is_received()
    {
        Assert.Null(LedgerCalculator.StatusOf(null, 0m));
        Assert.Equal(CommissionStatus.Expected, LedgerCalculator.StatusOf(140m, 0m));
        Assert.Equal(CommissionStatus.PartiallyReceived, LedgerCalculator.StatusOf(140m, 40m));
        Assert.Equal(CommissionStatus.Received, LedgerCalculator.StatusOf(140m, 140m));
        Assert.Equal(CommissionStatus.Expected, LedgerCalculator.StatusOf(0m, 0m));
    }
}
