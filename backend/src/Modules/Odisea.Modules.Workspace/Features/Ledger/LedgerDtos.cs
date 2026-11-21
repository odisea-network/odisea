using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Odisea.Modules.Workspace.Domain.Ledger;

namespace Odisea.Modules.Workspace.Features.Ledger;

public sealed record SaveTourOperatorRequest(
    [Required] string Name, string? ContactEmail, string? ContactPhone, string? Notes);

public sealed record TourOperatorDto(
    Guid Id, string Name, string? ContactEmail, string? ContactPhone, string? Notes, bool IsArchived);

public sealed record InstallmentRequest(LedgerInstallmentLabel Label, decimal Amount, DateOnly DueDate);

/// Percent: Rate plus Base (OperatorTotal or CustomerSale). Fixed, or Base = Manual: Amount.
public sealed record CommissionRequest(CommissionType Type, decimal? Rate, CommissionBase? Base, decimal? Amount);

public sealed record CreateLedgerReservationRequest(
    Guid TourOperatorId,
    [Required] string Reference,
    [Required] string HolderName,
    DateOnly BookedOn,
    DateOnly TravelStart,
    DateOnly? TravelEnd,
    decimal OperatorTotal,
    string? Currency,
    decimal? CustomerSaleAmount,
    LedgerReservationStatus? Status,
    string? Notes,
    CommissionRequest? Commission,
    List<InstallmentRequest>? Installments);

/// Version is the value last read; a stale one is answered with 409. Reason is required when the
/// operator price or currency changes after payments were recorded.
public sealed record UpdateLedgerReservationRequest(
    uint Version,
    Guid TourOperatorId,
    [Required] string Reference,
    [Required] string HolderName,
    DateOnly BookedOn,
    DateOnly TravelStart,
    DateOnly? TravelEnd,
    decimal OperatorTotal,
    string? Currency,
    decimal? CustomerSaleAmount,
    string? Notes,
    CommissionRequest? Commission,
    string? Reason);

public sealed record ReplaceScheduleRequest(uint Version, [Required] List<InstallmentRequest> Installments, string? Reason);

public sealed record RecordLedgerPaymentRequest(
    decimal Amount,
    string? Currency,
    DateOnly PaidOn,
    LedgerPaymentMethod Method,
    string? Reference,
    string? Note,
    Guid? IdempotencyKey);

public sealed record VoidLedgerPaymentRequest([Required] string Reason);

public sealed record ChangeLedgerStatusRequest(uint Version, LedgerReservationStatus Status, string? Note);

public sealed record SetCommissionReceivedRequest(uint Version, decimal ReceivedAmount);

public sealed record LedgerInstallmentDto(
    Guid Id,
    LedgerInstallmentLabel Label,
    decimal Amount,
    DateOnly DueDate,
    int SortOrder,
    decimal PaidAmount,
    decimal RemainingAmount,
    InstallmentPaymentStatus Status,
    bool IsOverdue);

public sealed record LedgerPaymentDto(
    Guid Id,
    decimal Amount,
    string Currency,
    DateOnly PaidOn,
    LedgerPaymentMethod Method,
    string? Reference,
    string? Note,
    Guid RecordedByUserId,
    DateTimeOffset CreatedAt,
    bool IsVoided,
    DateTimeOffset? VoidedAt,
    string? VoidReason);

public sealed record LedgerCommissionDto(
    CommissionType Type,
    decimal? Rate,
    CommissionBase Base,
    decimal? ExpectedAmount,
    decimal ReceivedAmount,
    CommissionStatus? Status);

public sealed record LedgerReservationDto(
    Guid Id,
    string Reference,
    Guid TourOperatorId,
    string TourOperatorName,
    string HolderName,
    DateOnly BookedOn,
    DateOnly TravelStart,
    DateOnly? TravelEnd,
    decimal OperatorTotal,
    string Currency,
    decimal? CustomerSaleAmount,
    LedgerReservationStatus Status,
    string? Notes,
    string? CancellationNote,
    LedgerCommissionDto? Commission,
    decimal ScheduledTotal,
    decimal ScheduleDifference,
    decimal PaidTotal,
    decimal Outstanding,
    decimal Overpaid,
    decimal OverdueAmount,
    DateOnly? NextDueDate,
    List<LedgerInstallmentDto> Installments,
    List<LedgerPaymentDto> Payments,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record LedgerReservationSummaryDto(
    Guid Id,
    string Reference,
    Guid TourOperatorId,
    string TourOperatorName,
    string HolderName,
    DateOnly TravelStart,
    DateOnly? TravelEnd,
    decimal OperatorTotal,
    string Currency,
    decimal PaidTotal,
    decimal Outstanding,
    decimal OverdueAmount,
    DateOnly? NextDueDate,
    decimal? ExpectedCommission,
    LedgerReservationStatus Status);

public sealed record LedgerAuditEventDto(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string Action,
    string ActorType,
    Guid ActorId,
    string? Reason,
    JsonElement Changes,
    DateTimeOffset OccurredAt);

public static class LedgerMapping
{
    public static TourOperatorDto ToDto(this TourOperator o) =>
        new(o.Id, o.Name, o.ContactEmail, o.ContactPhone, o.Notes, o.IsArchived);

    public static LedgerCommissionDto? ToCommissionDto(this LedgerReservation r) =>
        r.CommissionType is not { } type
            ? null
            : new(type, r.CommissionRate, r.CommissionBase ?? CommissionBase.Manual, r.CommissionExpected,
                r.CommissionReceived, LedgerCalculator.StatusOf(r.CommissionExpected, r.CommissionReceived));

    public static LedgerReservationDto ToDto(this LedgerReservation r, string operatorName, DateOnly today)
    {
        var totals = LedgerCalculator.Compute(r, today);
        return new(
            r.Id, r.Reference, r.TourOperatorId, operatorName, r.HolderName, r.BookedOn, r.TravelStart, r.TravelEnd,
            r.OperatorTotal, r.Currency, r.CustomerSaleAmount, r.Status, r.Notes, r.CancellationNote,
            r.ToCommissionDto(),
            totals.ScheduledTotal, totals.ScheduleDifference, totals.PaidTotal, totals.Outstanding, totals.Overpaid,
            totals.OverdueAmount, totals.NextDueDate,
            [.. r.Installments.OrderBy(i => i.SortOrder).Select(i =>
            {
                var state = totals.Installments[i.Id];
                return new LedgerInstallmentDto(i.Id, i.Label, i.Amount, i.DueDate, i.SortOrder,
                    state.Paid, state.Remaining, state.Status, state.IsOverdue);
            })],
            [.. r.Payments.OrderBy(p => p.PaidOn).ThenBy(p => p.CreatedAt).Select(p => new LedgerPaymentDto(
                p.Id, p.Amount, p.Currency, p.PaidOn, p.Method, p.Reference, p.Note, p.RecordedByUserId, p.CreatedAt,
                p.IsVoided, p.VoidedAt, p.VoidReason))],
            r.Version, r.CreatedAt, r.UpdatedAt);
    }

    public static LedgerReservationSummaryDto ToSummaryDto(this LedgerReservation r, string operatorName, DateOnly today)
    {
        var totals = LedgerCalculator.Compute(r, today);
        return new(
            r.Id, r.Reference, r.TourOperatorId, operatorName, r.HolderName, r.TravelStart, r.TravelEnd,
            r.OperatorTotal, r.Currency, totals.PaidTotal, totals.Outstanding, totals.OverdueAmount, totals.NextDueDate,
            r.CommissionExpected, r.Status);
    }

    public static LedgerAuditEventDto ToDto(this LedgerAuditEvent e) =>
        new(e.Id, e.EntityType, e.EntityId, e.Action, e.ActorType, e.ActorId, e.Reason,
            JsonDocument.Parse(e.Changes).RootElement.Clone(), e.CreatedAt);
}
