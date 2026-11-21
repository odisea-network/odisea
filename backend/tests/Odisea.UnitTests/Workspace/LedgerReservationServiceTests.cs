using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Workspace.Domain;
using Odisea.Modules.Workspace.Domain.Ledger;
using Odisea.Modules.Workspace.Features.Ledger;
using Odisea.Modules.Workspace.Infrastructure;
using Odisea.SharedKernel;
using Xunit;

namespace Odisea.UnitTests.Workspace;

public class LedgerReservationServiceTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 12, 10, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeEntitlements : IAgencyEntitlements
    {
        public HashSet<Guid> WithLedger { get; } = [];

        public Task<AgencyEntitlements?> GetAsync(Guid agencyId, CancellationToken ct) =>
            Task.FromResult<AgencyEntitlements?>(null);

        public Task RequireAsync(Guid agencyId, AgencyFeature feature, CancellationToken ct) =>
            WithLedger.Contains(agencyId) && feature == AgencyFeature.ReservationLedger
                ? Task.CompletedTask
                : throw new FeatureNotInPlanException(feature, "Core");
    }

    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly WorkspaceDbContext _db;
    private readonly FakeClock _clock = new();
    private readonly FakeEntitlements _entitlements = new();
    private readonly LedgerReservationService _ledger;
    private readonly TourOperatorService _operators;
    private readonly Guid _agencyId = Guid.NewGuid();
    private readonly Guid _otherAgencyId = Guid.NewGuid();
    private readonly LedgerActor _agent = new(Guid.NewGuid(), WorkspaceActor.Agency, IsAdmin: false);
    private readonly LedgerActor _admin = new(Guid.NewGuid(), WorkspaceActor.Agency, IsAdmin: true);

    public LedgerReservationServiceTests()
    {
        _db = new WorkspaceDbContext(new DbContextOptionsBuilder<WorkspaceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _ledger = new LedgerReservationService(_db, _entitlements, _clock, Options.Create(new LedgerOptions()),
            NullLogger<LedgerReservationService>.Instance);
        _operators = new TourOperatorService(_db, _entitlements, _clock);
        _entitlements.WithLedger.Add(_agencyId);
        _entitlements.WithLedger.Add(_otherAgencyId);
    }

    private async Task<Guid> OperatorAsync(string name = "Alfa Tours", Guid? agencyId = null) =>
        (await _operators.CreateAsync(agencyId ?? _agencyId, new SaveTourOperatorRequest(name, null, null, null), Ct)).Id;

    private static List<InstallmentRequest> DepositAndBalance() =>
    [
        new(LedgerInstallmentLabel.Deposit, 600m, new DateOnly(2026, 12, 1)),
        new(LedgerInstallmentLabel.Balance, 1400m, new DateOnly(2027, 1, 15)),
    ];

    private async Task<LedgerReservationDto> CreateAsync(
        string reference = "TO-1001", Guid? operatorId = null, Guid? agencyId = null, decimal total = 2000m,
        string? currency = null, CommissionRequest? commission = null, List<InstallmentRequest>? installments = null,
        decimal? customerSale = null) =>
        await _ledger.CreateAsync(agencyId ?? _agencyId, _agent, new CreateLedgerReservationRequest(
            operatorId ?? await OperatorAsync($"Operator {Guid.NewGuid():N}", agencyId), reference, "Georgi Ivanov",
            new DateOnly(2026, 11, 20), new DateOnly(2027, 2, 1), new DateOnly(2027, 2, 8), total, currency, customerSale,
            null, "window seats", commission, installments ?? DepositAndBalance()), Ct);

    private Task<LedgerReservationDto> PayAsync(Guid id, decimal amount, Guid? key = null, string? currency = null, DateOnly? on = null) =>
        _ledger.RecordPaymentAsync(id, _agencyId, _agent, new RecordLedgerPaymentRequest(
            amount, currency, on ?? new DateOnly(2026, 12, 5), LedgerPaymentMethod.BankTransfer, "REF-1", null, key), Ct);

    private static UpdateLedgerReservationRequest UpdateOf(LedgerReservationDto dto, decimal? total = null, string? currency = null,
        string? reason = null, CommissionRequest? commission = null, string? reference = null) =>
        new(dto.Version, dto.TourOperatorId, reference ?? dto.Reference, dto.HolderName, dto.BookedOn, dto.TravelStart, dto.TravelEnd,
            total ?? dto.OperatorTotal, currency ?? dto.Currency, dto.CustomerSaleAmount, dto.Notes, commission, reason);

    private static async Task<string> CodeAsync(Func<Task> act) =>
        (await Assert.ThrowsAsync<LedgerValidationException>(act)).Code;

    [Fact]
    public async Task Create_then_part_pay_shows_the_brief_example_figures()
    {
        var created = await CreateAsync(commission: new CommissionRequest(CommissionType.Percent, 7m, CommissionBase.OperatorTotal, null));

        Assert.Equal("EUR", created.Currency);
        Assert.Equal(LedgerReservationStatus.Confirmed, created.Status);
        Assert.Equal(2000m, created.Outstanding);
        Assert.Equal(140m, created.Commission!.ExpectedAmount);
        Assert.Equal(CommissionStatus.Expected, created.Commission.Status);

        var paid = await PayAsync(created.Id, 400m);

        Assert.Equal(400m, paid.PaidTotal);
        Assert.Equal(1600m, paid.Outstanding);
        Assert.Equal(200m, paid.Installments[0].RemainingAmount);
        Assert.Equal(InstallmentPaymentStatus.PartiallyPaid, paid.Installments[0].Status);
        Assert.True(paid.Installments[0].IsOverdue);
        Assert.Equal(InstallmentPaymentStatus.Unpaid, paid.Installments[1].Status);
        // Expected commission is neither received nor deducted from what the operator is owed.
        Assert.Equal(0m, paid.Commission!.ReceivedAmount);
        Assert.Equal(LedgerReservationStatus.Confirmed, paid.Status);

        var listed = Assert.Single((await _ledger.ListAsync(_agencyId, "georgi", null, null, null, null, null, 1, 50, Ct)).Items);
        Assert.Equal(1600m, listed.Outstanding);
        Assert.Equal(200m, listed.OverdueAmount);
        Assert.Equal(new DateOnly(2026, 12, 1), listed.NextDueDate);
    }

    [Fact]
    public async Task Writes_are_gated_by_the_plan_but_reads_survive_a_downgrade()
    {
        var created = await CreateAsync();
        _entitlements.WithLedger.Remove(_agencyId);

        await Assert.ThrowsAsync<FeatureNotInPlanException>(() => PayAsync(created.Id, 10m));
        await Assert.ThrowsAsync<FeatureNotInPlanException>(() => CreateAsync("TO-2"));
        Assert.Equal(created.Id, (await _ledger.GetAsync(created.Id, _agencyId, Ct)).Id);
    }

    [Fact]
    public async Task Another_agency_sees_nothing_and_cannot_touch_anything()
    {
        var created = await PayAsync((await CreateAsync()).Id, 100m);
        var paymentId = created.Payments[0].Id;

        await Assert.ThrowsAsync<LedgerNotFoundException>(() => _ledger.GetAsync(created.Id, _otherAgencyId, Ct));
        await Assert.ThrowsAsync<LedgerNotFoundException>(() => _ledger.HistoryAsync(created.Id, _otherAgencyId, Ct));
        await Assert.ThrowsAsync<LedgerNotFoundException>(() => _ledger.RecordPaymentAsync(created.Id, _otherAgencyId, _agent,
            new RecordLedgerPaymentRequest(10m, null, new DateOnly(2026, 12, 5), LedgerPaymentMethod.Cash, null, null, null), Ct));
        await Assert.ThrowsAsync<LedgerNotFoundException>(() => _ledger.VoidPaymentAsync(created.Id, paymentId, _otherAgencyId, _admin,
            new VoidLedgerPaymentRequest("mistake"), Ct));
        await Assert.ThrowsAsync<LedgerNotFoundException>(() => _ledger.UpdateAsync(created.Id, _otherAgencyId, _admin, UpdateOf(created), Ct));
        Assert.Empty((await _ledger.ListAsync(_otherAgencyId, null, null, null, null, null, null, 1, 50, Ct)).Items);
        Assert.Empty(await _operators.ListAsync(_otherAgencyId, includeArchived: true, Ct));
    }

    [Fact]
    public async Task A_reservation_cannot_use_another_agencys_operator()
    {
        var foreign = await OperatorAsync("Foreign", _otherAgencyId);

        Assert.Equal("operator_unknown", await CodeAsync(() => CreateAsync(operatorId: foreign)));
    }

    [Fact]
    public async Task References_are_unique_per_operator_within_an_agency()
    {
        var alfa = await OperatorAsync("Alfa");
        var beta = await OperatorAsync("Beta");
        await CreateAsync("X-1", alfa);

        var conflict = await Assert.ThrowsAsync<LedgerConflictException>(() => CreateAsync("X-1", alfa));
        Assert.Equal("reference_taken", conflict.Code);
        await CreateAsync("X-1", beta);
        await CreateAsync("X-1", agencyId: _otherAgencyId);
    }

    [Fact]
    public async Task Payments_must_be_positive_in_the_reservation_currency_and_not_in_the_future()
    {
        var created = await CreateAsync();

        Assert.Equal("amount_invalid", await CodeAsync(() => PayAsync(created.Id, 0m)));
        Assert.Equal("amount_invalid", await CodeAsync(() => PayAsync(created.Id, 10.005m)));
        Assert.Equal("currency_mismatch", await CodeAsync(() => PayAsync(created.Id, 10m, currency: "USD")));
        Assert.Equal("paid_on_in_future", await CodeAsync(() => PayAsync(created.Id, 10m, on: new DateOnly(2026, 12, 11))));
        Assert.Empty(_db.LedgerPayments);
    }

    [Fact]
    public async Task Today_follows_the_agency_time_zone_not_utc()
    {
        var created = await CreateAsync();
        // 23:30 UTC on the 10th is already the 11th in Sofia.
        _clock.UtcNow = new DateTimeOffset(2026, 12, 10, 23, 30, 0, TimeSpan.Zero);

        await PayAsync(created.Id, 10m, on: new DateOnly(2026, 12, 11));
    }

    [Fact]
    public async Task A_retried_payment_with_the_same_key_is_recorded_once()
    {
        var created = await CreateAsync();
        var key = Guid.NewGuid();

        await PayAsync(created.Id, 400m, key);
        var again = await PayAsync(created.Id, 400m, key);

        Assert.Equal(400m, again.PaidTotal);
        Assert.Single(again.Payments);
    }

    [Fact]
    public async Task Overpaying_is_accepted_and_flagged()
    {
        var created = await CreateAsync(total: 500m, installments: []);

        var paid = await PayAsync(created.Id, 620m);

        Assert.Equal(0m, paid.Outstanding);
        Assert.Equal(120m, paid.Overpaid);
    }

    [Fact]
    public async Task Only_an_admin_voids_a_payment_and_the_record_stays()
    {
        var paid = await PayAsync((await CreateAsync()).Id, 400m);
        var paymentId = paid.Payments[0].Id;

        await Assert.ThrowsAsync<LedgerAdminRequiredException>(() => _ledger.VoidPaymentAsync(
            paid.Id, paymentId, _agencyId, _agent, new VoidLedgerPaymentRequest("typo"), Ct));
        Assert.Equal("reason_required", await CodeAsync(() => _ledger.VoidPaymentAsync(
            paid.Id, paymentId, _agencyId, _admin, new VoidLedgerPaymentRequest("  "), Ct)));

        var voided = await _ledger.VoidPaymentAsync(paid.Id, paymentId, _agencyId, _admin, new VoidLedgerPaymentRequest("typo"), Ct);

        Assert.Equal(0m, voided.PaidTotal);
        Assert.Equal(2000m, voided.Outstanding);
        var payment = Assert.Single(voided.Payments);
        Assert.True(payment.IsVoided);
        Assert.Equal("typo", payment.VoidReason);
        await Assert.ThrowsAsync<LedgerConflictException>(() => _ledger.VoidPaymentAsync(
            paid.Id, paymentId, _agencyId, _admin, new VoidLedgerPaymentRequest("again"), Ct));
    }

    [Fact]
    public async Task Price_and_schedule_are_free_to_edit_until_money_has_moved()
    {
        var created = await CreateAsync();

        var repriced = await _ledger.UpdateAsync(created.Id, _agencyId, _agent, UpdateOf(created, total: 2100m), Ct);
        var rescheduled = await _ledger.ReplaceScheduleAsync(created.Id, _agencyId, _agent, new ReplaceScheduleRequest(
            repriced.Version, [new(LedgerInstallmentLabel.Other, 2100m, new DateOnly(2027, 1, 5))], null), Ct);

        Assert.Equal(2100m, rescheduled.OperatorTotal);
        var only = Assert.Single(rescheduled.Installments);
        Assert.Equal(2100m, only.Amount);
        Assert.Equal(0m, rescheduled.ScheduleDifference);
        Assert.Equal(1, await _db.LedgerInstallments.CountAsync());
    }

    [Fact]
    public async Task After_a_payment_price_and_schedule_changes_need_an_admin_and_a_reason()
    {
        var paid = await PayAsync((await CreateAsync()).Id, 400m);
        var schedule = new List<InstallmentRequest> { new(LedgerInstallmentLabel.Balance, 1900m, new DateOnly(2027, 1, 20)) };

        await Assert.ThrowsAsync<LedgerAdminRequiredException>(() =>
            _ledger.UpdateAsync(paid.Id, _agencyId, _agent, UpdateOf(paid, total: 1900m, reason: "discount"), Ct));
        Assert.Equal("reason_required", await CodeAsync(() =>
            _ledger.UpdateAsync(paid.Id, _agencyId, _admin, UpdateOf(paid, total: 1900m), Ct)));
        Assert.Equal("currency_locked", await CodeAsync(() =>
            _ledger.UpdateAsync(paid.Id, _agencyId, _admin, UpdateOf(paid, currency: "USD", reason: "wrong currency"), Ct)));
        await Assert.ThrowsAsync<LedgerAdminRequiredException>(() =>
            _ledger.ReplaceScheduleAsync(paid.Id, _agencyId, _agent, new ReplaceScheduleRequest(paid.Version, schedule, "new dates"), Ct));

        var repriced = await _ledger.UpdateAsync(paid.Id, _agencyId, _admin, UpdateOf(paid, total: 1900m, reason: "operator discount"), Ct);
        var rescheduled = await _ledger.ReplaceScheduleAsync(paid.Id, _agencyId, _admin,
            new ReplaceScheduleRequest(repriced.Version, schedule, "operator moved the date"), Ct);

        Assert.Equal(1500m, rescheduled.Outstanding);
        Assert.Equal(400m, rescheduled.PaidTotal);
        Assert.Equal(400m, Assert.Single(rescheduled.Installments).PaidAmount);

        // An agent may still edit what is not money.
        var renamed = await _ledger.UpdateAsync(paid.Id, _agencyId, _agent, UpdateOf(rescheduled, reference: "TO-1001-A"), Ct);
        Assert.Equal("TO-1001-A", renamed.Reference);
    }

    [Fact]
    public async Task Every_money_change_is_audited_with_actor_and_reason_but_without_names()
    {
        var created = await CreateAsync();
        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        var paid = await PayAsync(created.Id, 400m);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        await _ledger.UpdateAsync(paid.Id, _agencyId, _admin, UpdateOf(paid, total: 1900m, reason: "operator discount"), Ct);

        var history = await _ledger.HistoryAsync(paid.Id, _agencyId, Ct);

        Assert.Equal(["Updated", "PaymentRecorded", "Created"], history.Select(e => e.Action));
        var update = history[0];
        Assert.Equal(_admin.Id, update.ActorId);
        Assert.Equal("operator discount", update.Reason);
        Assert.Equal(2000m, update.Changes.GetProperty("operatorTotal").GetProperty("from").GetDecimal());
        Assert.Equal(1900m, update.Changes.GetProperty("operatorTotal").GetProperty("to").GetDecimal());
        Assert.Equal("Payment", history[1].EntityType);
        Assert.Equal(400m, history[1].Changes.GetProperty("amount").GetProperty("to").GetDecimal());
        Assert.DoesNotContain("Georgi", string.Concat(history.Select(e => e.Changes.GetRawText())));
    }

    [Fact]
    public async Task Cancelling_needs_a_note_and_leaves_the_figures_alone()
    {
        var paid = await PayAsync((await CreateAsync()).Id, 400m);

        Assert.Equal("cancellation_note_required", await CodeAsync(() => _ledger.ChangeStatusAsync(
            paid.Id, _agencyId, _agent, new ChangeLedgerStatusRequest(paid.Version, LedgerReservationStatus.Cancelled, null), Ct)));
        var cancelled = await _ledger.ChangeStatusAsync(paid.Id, _agencyId, _agent,
            new ChangeLedgerStatusRequest(paid.Version, LedgerReservationStatus.Cancelled, "customer withdrew"), Ct);

        Assert.Equal(LedgerReservationStatus.Cancelled, cancelled.Status);
        Assert.Equal("customer withdrew", cancelled.CancellationNote);
        Assert.Equal(1600m, cancelled.Outstanding);
        Assert.Equal(400m, cancelled.PaidTotal);
        Assert.Equal(2, cancelled.Installments.Count);
    }

    [Fact]
    public async Task Commission_rules_reject_incomplete_input_and_track_what_was_received()
    {
        Assert.Equal("commission_rate_invalid", await CodeAsync(() =>
            CreateAsync("C-1", commission: new CommissionRequest(CommissionType.Percent, 0m, CommissionBase.OperatorTotal, null))));
        Assert.Equal("commission_base_missing", await CodeAsync(() =>
            CreateAsync("C-2", commission: new CommissionRequest(CommissionType.Percent, 7m, CommissionBase.CustomerSale, null))));
        Assert.Equal("commission_amount_required", await CodeAsync(() =>
            CreateAsync("C-3", commission: new CommissionRequest(CommissionType.Fixed, null, null, null))));

        var plain = await CreateAsync("C-4");
        Assert.Null(plain.Commission);
        Assert.Equal("commission_not_set", await CodeAsync(() => _ledger.SetCommissionReceivedAsync(
            plain.Id, _agencyId, _agent, new SetCommissionReceivedRequest(plain.Version, 10m), Ct)));

        var withFixed = await CreateAsync("C-5", commission: new CommissionRequest(CommissionType.Fixed, null, null, 150m));
        Assert.Equal(CommissionBase.Manual, withFixed.Commission!.Base);
        var part = await _ledger.SetCommissionReceivedAsync(withFixed.Id, _agencyId, _agent,
            new SetCommissionReceivedRequest(withFixed.Version, 50m), Ct);
        Assert.Equal(CommissionStatus.PartiallyReceived, part.Commission!.Status);
        Assert.Equal(2000m, part.Outstanding);
        Assert.Equal("commission_received_exists", await CodeAsync(() =>
            _ledger.UpdateAsync(part.Id, _agencyId, _agent, UpdateOf(part, commission: null), Ct)));
    }

    [Fact]
    public async Task Invalid_reservations_are_rejected_with_a_code()
    {
        var op = await OperatorAsync();
        CreateLedgerReservationRequest Request(string reference = "R-1", string holder = "Ana", decimal total = 100m,
            string? currency = null, DateOnly? end = null, List<InstallmentRequest>? installments = null) =>
            new(op, reference, holder, new DateOnly(2026, 11, 1), new DateOnly(2027, 3, 1), end, total, currency, null, null, null, null, installments);
        Task Create(CreateLedgerReservationRequest request) => _ledger.CreateAsync(_agencyId, _agent, request, Ct);

        Assert.Equal("reference_required", await CodeAsync(() => Create(Request(reference: " "))));
        Assert.Equal("holderName_required", await CodeAsync(() => Create(Request(holder: ""))));
        Assert.Equal("operatorTotal_invalid", await CodeAsync(() => Create(Request(total: -1m))));
        Assert.Equal("currency_invalid", await CodeAsync(() => Create(Request(currency: "EURO"))));
        Assert.Equal("travel_end_before_start", await CodeAsync(() => Create(Request(end: new DateOnly(2027, 2, 1)))));
        Assert.Equal("installmentAmount_invalid", await CodeAsync(() => Create(Request(
            installments: [new(LedgerInstallmentLabel.Deposit, 0m, new DateOnly(2026, 12, 1))]))));
        Assert.Equal("too_many_installments", await CodeAsync(() => Create(Request(
            installments: [.. Enumerable.Range(1, 25).Select(d => new InstallmentRequest(LedgerInstallmentLabel.Other, 1m, new DateOnly(2027, 1, d)))]))));
        Assert.Empty(_db.LedgerReservations);
    }

    [Fact]
    public async Task Operators_are_unique_by_name_per_agency_and_only_admins_archive_them()
    {
        var id = await OperatorAsync("Alfa Tours");

        var conflict = await Assert.ThrowsAsync<LedgerConflictException>(() => OperatorAsync(" alfa tours "));
        Assert.Equal("operator_name_taken", conflict.Code);
        await OperatorAsync("Alfa Tours", _otherAgencyId);

        await Assert.ThrowsAsync<LedgerAdminRequiredException>(() => _operators.SetArchivedAsync(id, _agencyId, _agent, true, Ct));
        await Assert.ThrowsAsync<LedgerNotFoundException>(() => _operators.SetArchivedAsync(id, _otherAgencyId, _admin, true, Ct));
        await _operators.SetArchivedAsync(id, _agencyId, _admin, true, Ct);

        Assert.Empty(await _operators.ListAsync(_agencyId, includeArchived: false, Ct));
        Assert.Single(await _operators.ListAsync(_agencyId, includeArchived: true, Ct));
        Assert.Equal("operator_archived", await CodeAsync(() => CreateAsync(operatorId: id)));
    }

    [Fact]
    public async Task List_filters_by_status_operator_travel_dates_and_search_text()
    {
        var alfa = await OperatorAsync("Alfa Tours");
        var beta = await OperatorAsync("Beta Travel");
        var first = await CreateAsync("AAA-1", alfa);
        await CreateAsync("BBB-2", beta);
        await _ledger.ChangeStatusAsync(first.Id, _agencyId, _agent,
            new ChangeLedgerStatusRequest(first.Version, LedgerReservationStatus.Completed, null), Ct);

        Task<int> Count(string? q = null, LedgerReservationStatus? status = null, Guid? op = null, DateOnly? from = null, DateOnly? to = null) =>
            _ledger.ListAsync(_agencyId, q, status, op, from, to, null, 1, 50, Ct).ContinueWith(t => t.Result.TotalCount);

        Assert.Equal(2, await Count());
        Assert.Equal(1, await Count(q: "bbb"));
        Assert.Equal(1, await Count(q: "beta tr"));
        Assert.Equal(1, await Count(status: LedgerReservationStatus.Completed));
        Assert.Equal(1, await Count(op: alfa));
        Assert.Equal(2, await Count(from: new DateOnly(2027, 2, 1), to: new DateOnly(2027, 2, 1)));
        Assert.Equal(0, await Count(from: new DateOnly(2027, 2, 2)));
    }
}
