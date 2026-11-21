using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Workspace.Domain.Ledger;
using Odisea.Modules.Workspace.Features.ExternalReservations;
using Odisea.Modules.Workspace.Infrastructure;
using Odisea.SharedKernel;

namespace Odisea.Modules.Workspace.Features.Ledger;

/// Everything is scoped by the caller's agency id (from the token, never from the request); someone
/// else's reservation is indistinguishable from a missing one. Writes need the ReservationLedger
/// feature; reads keep working after a downgrade so an agency can always see what it owes.
public sealed class LedgerReservationService(
    WorkspaceDbContext db,
    IAgencyEntitlements entitlements,
    IClock clock,
    IOptions<LedgerOptions> options,
    ILogger<LedgerReservationService> logger)
{
    private const int MaxPageSize = 200;
    private const int MaxInstallments = 24;

    private readonly TimeZoneInfo _zone = options.Value.ResolveZone();

    private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, _zone).DateTime);

    public async Task<LedgerReservationDto> CreateAsync(
        Guid agencyId, LedgerActor actor, CreateLedgerReservationRequest request, CancellationToken ct)
    {
        await entitlements.RequireAsync(agencyId, AgencyFeature.ReservationLedger, ct);

        var tourOperator = await OperatorAsync(agencyId, request.TourOperatorId, ct);
        if (tourOperator.IsArchived)
            throw new LedgerValidationException("operator_archived", "This tour operator is archived.");

        var now = clock.UtcNow;
        var reservation = new LedgerReservation
        {
            AgencyId = agencyId,
            TourOperatorId = tourOperator.Id,
            Reference = LedgerRules.Required(request.Reference, 50, "reference"),
            HolderName = LedgerRules.Required(request.HolderName, 200, "holderName"),
            Currency = LedgerRules.Currency(request.Currency),
            Status = request.Status ?? LedgerReservationStatus.Confirmed,
            CreatedByUserId = actor.Id,
            CreatedAt = now,
            UpdatedAt = now,
        };
        if (reservation.Status == LedgerReservationStatus.Cancelled)
            throw new LedgerValidationException("status_invalid", "A reservation cannot be created as cancelled.");
        await EnsureReferenceFreeAsync(agencyId, reservation.TourOperatorId, reservation.Reference, except: null, ct);

        ApplyDatesAndAmounts(reservation, request.BookedOn, request.TravelStart, request.TravelEnd,
            request.OperatorTotal, request.CustomerSaleAmount, request.Notes);
        ApplyCommission(reservation, request.Commission);
        reservation.Installments.AddRange(BuildSchedule(reservation, request.Installments ?? []));

        db.LedgerReservations.Add(reservation);
        Audit(reservation, "Reservation", reservation.Id, "Created", actor, reason: null, new LedgerChanges()
            .Set("operatorTotal", reservation.OperatorTotal)
            .Set("currency", reservation.Currency)
            .Set("commissionExpected", reservation.CommissionExpected)
            .Set("installments", Describe(reservation.Installments)));
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Ledger reservation {ReservationId} created for agency {AgencyId}", reservation.Id, agencyId);
        return reservation.ToDto(tourOperator.Name, Today);
    }

    public async Task<LedgerReservationDto> GetAsync(Guid id, Guid agencyId, CancellationToken ct)
    {
        var reservation = await LoadAsync(id, agencyId, tracking: false, ct);
        return reservation.ToDto(await OperatorNameAsync(reservation, ct), Today);
    }

    public async Task<PagedResult<LedgerReservationSummaryDto>> ListAsync(
        Guid agencyId, string? search, LedgerReservationStatus? status, Guid? operatorId,
        DateOnly? travelFrom, DateOnly? travelTo, int? dueWithinDays, bool overdueOnly,
        string? sort, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var today = Today;

        var query = db.LedgerReservations.AsNoTracking().Where(r => r.AgencyId == agencyId);
        if (status is { } s)
            query = query.Where(r => r.Status == s);
        if (operatorId is { } o)
            query = query.Where(r => r.TourOperatorId == o);
        if (travelFrom is { } from)
            query = query.Where(r => r.TravelStart >= from);
        if (travelTo is { } to)
            query = query.Where(r => r.TravelStart <= to);
        if (search?.Trim().ToLowerInvariant() is { Length: > 0 } q)
            query = query.Where(r =>
                r.Reference.ToLower().Contains(q)
                || r.HolderName.ToLower().Contains(q)
                || db.TourOperators.Any(t => t.Id == r.TourOperatorId && t.NameKey.Contains(q)));

        // What is still open on an instalment depends on how the payments fall across the schedule,
        // which SQL does not know. The due filters therefore narrow by date in the database and decide
        // on the computed figures in memory, over the agency's open reservations only.
        var dueWindow = DueWindow.For(today, dueWithinDays, overdueOnly);
        if (dueWindow is { } window)
            query = query.Where(r => r.Status != LedgerReservationStatus.Draft
                                     && r.Installments.Any(i => i.DueDate >= window.From && i.DueDate <= window.To));

        var sorted = sort switch
        {
            "travelStart" => query.OrderBy(r => r.TravelStart).ThenBy(r => r.Id),
            "-travelStart" => query.OrderByDescending(r => r.TravelStart).ThenBy(r => r.Id),
            "bookedOn" => query.OrderBy(r => r.BookedOn).ThenBy(r => r.Id),
            "-bookedOn" => query.OrderByDescending(r => r.BookedOn).ThenBy(r => r.Id),
            "reference" => query.OrderBy(r => r.Reference).ThenBy(r => r.Id),
            _ => null,
        };
        var ordered = sorted ?? query.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id);

        int total;
        List<LedgerReservation> rows;
        if (dueWindow is { } due)
        {
            var matches = (await ordered.Include(r => r.Installments).Include(r => r.Payments).AsSplitQuery().ToListAsync(ct))
                .Select(r => (Reservation: r, Totals: LedgerCalculator.Compute(r, today)))
                .Where(x => x.Reservation.Installments.Any(i =>
                    due.Contains(i.DueDate) && x.Totals.Installments[i.Id].Remaining > 0));
            // Unless asked otherwise, the most urgent first.
            if (sorted is null)
                matches = matches.OrderBy(x => x.Totals.NextDueDate).ThenBy(x => x.Reservation.Id);
            var all = matches.Select(x => x.Reservation).ToList();
            total = all.Count;
            rows = [.. all.Skip((page - 1) * pageSize).Take(pageSize)];
        }
        else
        {
            total = await query.CountAsync(ct);
            rows = await ordered
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Include(r => r.Installments).Include(r => r.Payments)
                .AsSplitQuery()
                .ToListAsync(ct);
        }

        var operatorIds = rows.Select(r => r.TourOperatorId).Distinct().ToList();
        var names = await db.TourOperators.AsNoTracking()
            .Where(t => t.AgencyId == agencyId && operatorIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        return new PagedResult<LedgerReservationSummaryDto>(
            [.. rows.Select(r => r.ToSummaryDto(names.GetValueOrDefault(r.TourOperatorId, ""), today))],
            page, pageSize, total);
    }

    public async Task<LedgerReservationDto> UpdateAsync(
        Guid id, Guid agencyId, LedgerActor actor, UpdateLedgerReservationRequest request, CancellationToken ct)
    {
        await entitlements.RequireAsync(agencyId, AgencyFeature.ReservationLedger, ct);

        var reservation = await LoadAsync(id, agencyId, tracking: true, ct);
        ExpectVersion(reservation, request.Version);

        var currency = LedgerRules.Currency(request.Currency);
        var total = LedgerRules.Amount(request.OperatorTotal, "operatorTotal", allowZero: true);
        string? reason = null;
        if (reservation.HasPayments && (total != reservation.OperatorTotal || currency != reservation.Currency))
        {
            // Recorded payments are in the old currency; they cannot be reinterpreted.
            if (currency != reservation.Currency)
                throw new LedgerValidationException("currency_locked",
                    "The currency cannot change while payments are recorded. Void the payments first.");
            reason = RequireAdminReason(actor, request.Reason,
                "Only an agency admin can change the operator price after payments were recorded.");
        }

        var tourOperator = await OperatorAsync(agencyId, request.TourOperatorId, ct);
        if (tourOperator.IsArchived && tourOperator.Id != reservation.TourOperatorId)
            throw new LedgerValidationException("operator_archived", "This tour operator is archived.");
        var reference = LedgerRules.Required(request.Reference, 50, "reference");
        if (reference != reservation.Reference || tourOperator.Id != reservation.TourOperatorId)
            await EnsureReferenceFreeAsync(agencyId, tourOperator.Id, reference, except: id, ct);
        var holderName = LedgerRules.Required(request.HolderName, 200, "holderName");

        var changes = new LedgerChanges()
            .Value("tourOperatorId", reservation.TourOperatorId, tourOperator.Id)
            .Value("reference", reservation.Reference, reference)
            .Text("holderName", reservation.HolderName, holderName)
            .Value("bookedOn", reservation.BookedOn, request.BookedOn)
            .Value("travelStart", reservation.TravelStart, request.TravelStart)
            .Value("travelEnd", reservation.TravelEnd, request.TravelEnd)
            .Value("operatorTotal", reservation.OperatorTotal, total)
            .Value("currency", reservation.Currency, currency)
            .Value("customerSaleAmount", reservation.CustomerSaleAmount, request.CustomerSaleAmount)
            .Text("notes", reservation.Notes, LedgerRules.Optional(request.Notes, 4000, "notes"));
        var commissionBefore = (reservation.CommissionType, reservation.CommissionRate, reservation.CommissionBase, reservation.CommissionExpected);

        reservation.TourOperatorId = tourOperator.Id;
        reservation.Reference = reference;
        reservation.HolderName = holderName;
        reservation.Currency = currency;
        ApplyDatesAndAmounts(reservation, request.BookedOn, request.TravelStart, request.TravelEnd,
            total, request.CustomerSaleAmount, request.Notes);
        ApplyCommission(reservation, request.Commission);

        changes
            .Value("commissionType", commissionBefore.CommissionType, reservation.CommissionType)
            .Value("commissionRate", commissionBefore.CommissionRate, reservation.CommissionRate)
            .Value("commissionBase", commissionBefore.CommissionBase, reservation.CommissionBase)
            .Value("commissionExpected", commissionBefore.CommissionExpected, reservation.CommissionExpected);

        if (changes.Any)
        {
            reservation.UpdatedAt = clock.UtcNow;
            Audit(reservation, "Reservation", reservation.Id, "Updated", actor, reason, changes);
            await db.SaveChangesAsync(ct);
        }
        return reservation.ToDto(tourOperator.Name, Today);
    }

    /// Replaces the whole schedule. The instalments need not add up to the operator price: the
    /// difference is reported, not rejected, because operators often confirm the dates later.
    public async Task<LedgerReservationDto> ReplaceScheduleAsync(
        Guid id, Guid agencyId, LedgerActor actor, ReplaceScheduleRequest request, CancellationToken ct)
    {
        await entitlements.RequireAsync(agencyId, AgencyFeature.ReservationLedger, ct);

        var reservation = await LoadAsync(id, agencyId, tracking: true, ct);
        ExpectVersion(reservation, request.Version);
        var reason = reservation.HasPayments
            ? RequireAdminReason(actor, request.Reason,
                "Only an agency admin can change the payment schedule after payments were recorded.")
            : null;

        var before = Describe(reservation.Installments);
        var replacement = BuildSchedule(reservation, request.Installments);

        db.LedgerInstallments.RemoveRange(reservation.Installments);
        reservation.Installments.Clear();
        // Added through the set, not the navigation: a child with a client-made key that EF first meets
        // through a tracked parent is taken for an existing row and updated instead of inserted.
        db.LedgerInstallments.AddRange(replacement);

        reservation.UpdatedAt = clock.UtcNow;
        Audit(reservation, "Reservation", reservation.Id, "ScheduleReplaced", actor, reason,
            new LedgerChanges().Value("installments", (object)before, Describe(replacement)));
        await db.SaveChangesAsync(ct);
        return await DtoAsync(reservation, ct);
    }

    /// Overpaying is allowed and reported: the transfer happened, hiding it would be worse.
    public async Task<LedgerReservationDto> RecordPaymentAsync(
        Guid id, Guid agencyId, LedgerActor actor, RecordLedgerPaymentRequest request, CancellationToken ct)
    {
        await entitlements.RequireAsync(agencyId, AgencyFeature.ReservationLedger, ct);

        var reservation = await LoadAsync(id, agencyId, tracking: true, ct);
        if (request.IdempotencyKey is { } key && reservation.Payments.Any(p => p.IdempotencyKey == key))
            return await DtoAsync(reservation, ct);

        var amount = LedgerRules.Amount(request.Amount, "amount", allowZero: false);
        if (LedgerRules.Currency(request.Currency ?? reservation.Currency) != reservation.Currency)
            throw new LedgerValidationException("currency_mismatch",
                $"The payment must be in the reservation's currency ({reservation.Currency}).");
        if (request.PaidOn > Today)
            throw new LedgerValidationException("paid_on_in_future", "A payment cannot be dated in the future.");

        var now = clock.UtcNow;
        var payment = new LedgerPayment
        {
            ReservationId = reservation.Id,
            AgencyId = agencyId,
            Amount = amount,
            Currency = reservation.Currency,
            PaidOn = request.PaidOn,
            Method = request.Method,
            Reference = LedgerRules.Optional(request.Reference, 100, "reference"),
            Note = LedgerRules.Optional(request.Note, 500, "note"),
            IdempotencyKey = request.IdempotencyKey,
            RecordedByUserId = actor.Id,
            CreatedAt = now,
        };
        db.LedgerPayments.Add(payment);

        // Touching the reservation row puts its xmin in the UPDATE, so two people recording against
        // the same stale balance cannot both succeed.
        reservation.UpdatedAt = now;
        Audit(reservation, "Payment", payment.Id, "PaymentRecorded", actor, reason: null, new LedgerChanges()
            .Set("amount", payment.Amount)
            .Set("currency", payment.Currency)
            .Set("paidOn", payment.PaidOn)
            .Set("method", payment.Method));
        await db.SaveChangesAsync(ct);
        return await DtoAsync(reservation, ct);
    }

    public async Task<LedgerReservationDto> VoidPaymentAsync(
        Guid id, Guid paymentId, Guid agencyId, LedgerActor actor, VoidLedgerPaymentRequest request, CancellationToken ct)
    {
        await entitlements.RequireAsync(agencyId, AgencyFeature.ReservationLedger, ct);

        var reservation = await LoadAsync(id, agencyId, tracking: true, ct);
        var payment = reservation.Payments.FirstOrDefault(p => p.Id == paymentId) ?? throw new LedgerNotFoundException();
        var reason = RequireAdminReason(actor, request.Reason, "Only an agency admin can void a payment.");
        if (payment.IsVoided)
            throw new LedgerConflictException("payment_already_voided", "This payment is already voided.");

        var now = clock.UtcNow;
        payment.VoidedAt = now;
        payment.VoidedByUserId = actor.Id;
        payment.VoidReason = reason;
        reservation.UpdatedAt = now;
        Audit(reservation, "Payment", payment.Id, "PaymentVoided", actor, reason,
            new LedgerChanges().Set("amount", payment.Amount).Set("paidOn", payment.PaidOn));
        await db.SaveChangesAsync(ct);
        return await DtoAsync(reservation, ct);
    }

    /// Cancelling changes nothing about what is owed: cancellation fees and refunds differ per
    /// operator, so the figures stay until someone corrects them on purpose.
    public async Task<LedgerReservationDto> ChangeStatusAsync(
        Guid id, Guid agencyId, LedgerActor actor, ChangeLedgerStatusRequest request, CancellationToken ct)
    {
        await entitlements.RequireAsync(agencyId, AgencyFeature.ReservationLedger, ct);

        var reservation = await LoadAsync(id, agencyId, tracking: true, ct);
        ExpectVersion(reservation, request.Version);
        if (reservation.Status == request.Status)
            return await DtoAsync(reservation, ct);

        var note = LedgerRules.Optional(request.Note, 500, "note");
        if (request.Status == LedgerReservationStatus.Cancelled && note is null)
            throw new LedgerValidationException("cancellation_note_required", "Say why the reservation is cancelled.");

        var changes = new LedgerChanges().Value("status", reservation.Status, request.Status);
        reservation.Status = request.Status;
        reservation.CancellationNote = request.Status == LedgerReservationStatus.Cancelled ? note : null;
        reservation.UpdatedAt = clock.UtcNow;
        Audit(reservation, "Reservation", reservation.Id, "StatusChanged", actor, note, changes);
        await db.SaveChangesAsync(ct);
        return await DtoAsync(reservation, ct);
    }

    public async Task<LedgerReservationDto> SetCommissionReceivedAsync(
        Guid id, Guid agencyId, LedgerActor actor, SetCommissionReceivedRequest request, CancellationToken ct)
    {
        await entitlements.RequireAsync(agencyId, AgencyFeature.ReservationLedger, ct);

        var reservation = await LoadAsync(id, agencyId, tracking: true, ct);
        ExpectVersion(reservation, request.Version);
        if (reservation.CommissionType is null)
            throw new LedgerValidationException("commission_not_set", "This reservation has no commission to receive.");

        var received = LedgerRules.Amount(request.ReceivedAmount, "receivedAmount", allowZero: true);
        if (received != reservation.CommissionReceived)
        {
            var changes = new LedgerChanges().Value("commissionReceived", reservation.CommissionReceived, received);
            reservation.CommissionReceived = received;
            reservation.UpdatedAt = clock.UtcNow;
            Audit(reservation, "Reservation", reservation.Id, "CommissionReceived", actor, reason: null, changes);
            await db.SaveChangesAsync(ct);
        }
        return await DtoAsync(reservation, ct);
    }

    public async Task<List<LedgerAuditEventDto>> HistoryAsync(Guid id, Guid agencyId, CancellationToken ct)
    {
        if (!await db.LedgerReservations.AnyAsync(r => r.Id == id && r.AgencyId == agencyId, ct))
            throw new LedgerNotFoundException();
        var events = await db.LedgerAuditEvents.AsNoTracking()
            .Where(e => e.AgencyId == agencyId && e.ReservationId == id)
            .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
            .ToListAsync(ct);
        return [.. events.Select(e => e.ToDto())];
    }

    private async Task<LedgerReservation> LoadAsync(Guid id, Guid agencyId, bool tracking, CancellationToken ct)
    {
        var query = db.LedgerReservations.Include(r => r.Installments).Include(r => r.Payments).AsSplitQuery();
        if (!tracking)
            query = query.AsNoTracking();
        return await query.FirstOrDefaultAsync(r => r.Id == id && r.AgencyId == agencyId, ct)
               ?? throw new LedgerNotFoundException();
    }

    private async Task<TourOperator> OperatorAsync(Guid agencyId, Guid operatorId, CancellationToken ct) =>
        await db.TourOperators.AsNoTracking().FirstOrDefaultAsync(o => o.Id == operatorId && o.AgencyId == agencyId, ct)
        ?? throw new LedgerValidationException("operator_unknown", "Pick one of the agency's tour operators.");

    private async Task<string> OperatorNameAsync(LedgerReservation reservation, CancellationToken ct) =>
        await db.TourOperators.AsNoTracking()
            .Where(o => o.Id == reservation.TourOperatorId && o.AgencyId == reservation.AgencyId)
            .Select(o => o.Name).FirstOrDefaultAsync(ct) ?? "";

    private async Task<LedgerReservationDto> DtoAsync(LedgerReservation reservation, CancellationToken ct) =>
        reservation.ToDto(await OperatorNameAsync(reservation, ct), Today);

    private async Task EnsureReferenceFreeAsync(Guid agencyId, Guid operatorId, string reference, Guid? except, CancellationToken ct)
    {
        if (await db.LedgerReservations.AnyAsync(
                r => r.AgencyId == agencyId && r.TourOperatorId == operatorId && r.Reference == reference && r.Id != except, ct))
            throw new LedgerConflictException("reference_taken",
                $"A reservation with number '{reference}' already exists for this tour operator.");
    }

    /// The token only exists on Postgres (xmin); the InMemory test double has no equivalent.
    private void ExpectVersion(LedgerReservation reservation, uint version)
    {
        if (db.Database.IsNpgsql())
            db.Entry(reservation).Property(r => r.Version).OriginalValue = version;
    }

    private static string RequireAdminReason(LedgerActor actor, string? reason, string whenNotAdmin)
    {
        if (!actor.IsAdmin)
            throw new LedgerAdminRequiredException(whenNotAdmin);
        return LedgerRules.Reason(reason);
    }

    private static void ApplyDatesAndAmounts(
        LedgerReservation reservation, DateOnly bookedOn, DateOnly travelStart, DateOnly? travelEnd,
        decimal operatorTotal, decimal? customerSaleAmount, string? notes)
    {
        if (bookedOn == default || travelStart == default)
            throw new LedgerValidationException("dates_required", "The booking date and the travel start date are required.");
        if (travelEnd is { } end && end < travelStart)
            throw new LedgerValidationException("travel_end_before_start", "The trip cannot end before it starts.");

        reservation.BookedOn = bookedOn;
        reservation.TravelStart = travelStart;
        reservation.TravelEnd = travelEnd;
        reservation.OperatorTotal = LedgerRules.Amount(operatorTotal, "operatorTotal", allowZero: true);
        reservation.CustomerSaleAmount = customerSaleAmount is { } sale
            ? LedgerRules.Amount(sale, "customerSaleAmount", allowZero: true)
            : null;
        reservation.Notes = LedgerRules.Optional(notes, 4000, "notes");
    }

    /// Commission is informational: it never reduces what is owed to the operator.
    private static void ApplyCommission(LedgerReservation reservation, CommissionRequest? request)
    {
        if (request is null)
        {
            if (reservation.CommissionReceived > 0)
                throw new LedgerValidationException("commission_received_exists",
                    "Commission was already recorded as received; set the received amount to zero before removing it.");
            reservation.CommissionType = null;
            reservation.CommissionRate = null;
            reservation.CommissionBase = null;
            reservation.CommissionExpected = null;
            return;
        }

        var commissionBase = request.Type == CommissionType.Fixed
            ? CommissionBase.Manual
            : request.Base ?? CommissionBase.OperatorTotal;
        decimal? rate = null;
        decimal? manual = null;
        if (commissionBase == CommissionBase.Manual)
        {
            manual = LedgerRules.Amount(
                request.Amount ?? throw new LedgerValidationException("commission_amount_required", "Enter the expected commission amount."),
                "commissionAmount", allowZero: true);
        }
        else
        {
            rate = request.Rate is > 0 and <= 100
                ? request.Rate
                : throw new LedgerValidationException("commission_rate_invalid", "The commission rate must be above 0 and at most 100 percent.");
            if (commissionBase == CommissionBase.CustomerSale && reservation.CustomerSaleAmount is null)
                throw new LedgerValidationException("commission_base_missing",
                    "Enter the customer sale price to take a commission percentage from it.");
        }
        // A percentage typed next to a manual amount is kept for reference only.
        if (commissionBase == CommissionBase.Manual && request.Type == CommissionType.Percent)
            rate = request.Rate is > 0 and <= 100 ? request.Rate : null;

        reservation.CommissionType = request.Type;
        reservation.CommissionRate = rate;
        reservation.CommissionBase = commissionBase;
        reservation.CommissionExpected = LedgerCalculator.ExpectedCommission(
            request.Type, rate, commissionBase, reservation.OperatorTotal, reservation.CustomerSaleAmount, manual);
    }

    private static List<LedgerInstallment> BuildSchedule(LedgerReservation reservation, List<InstallmentRequest> requests)
    {
        if (requests.Count > MaxInstallments)
            throw new LedgerValidationException("too_many_installments", $"A schedule may have at most {MaxInstallments} instalments.");
        return [.. requests.Select((r, index) => new LedgerInstallment
        {
            ReservationId = reservation.Id,
            AgencyId = reservation.AgencyId,
            Label = r.Label,
            Amount = LedgerRules.Amount(r.Amount, "installmentAmount", allowZero: false),
            DueDate = r.DueDate == default
                ? throw new LedgerValidationException("due_date_required", "Every instalment needs a due date.")
                : r.DueDate,
            SortOrder = index,
            CreatedAt = reservation.UpdatedAt,
        })];
    }

    private static List<object> Describe(IEnumerable<LedgerInstallment> installments) =>
        [.. installments.OrderBy(i => i.SortOrder).Select(i => (object)new { label = i.Label, amount = i.Amount, dueDate = i.DueDate })];

    private void Audit(
        LedgerReservation reservation, string entityType, Guid entityId, string action,
        LedgerActor actor, string? reason, LedgerChanges changes) =>
        db.LedgerAuditEvents.Add(new LedgerAuditEvent
        {
            AgencyId = reservation.AgencyId,
            ReservationId = reservation.Id,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            ActorType = actor.Type,
            ActorId = actor.Id,
            Reason = reason,
            Changes = changes.ToJson(),
            CreatedAt = clock.UtcNow,
        });
}
