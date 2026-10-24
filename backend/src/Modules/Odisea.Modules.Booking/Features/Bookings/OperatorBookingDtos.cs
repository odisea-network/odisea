using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Odisea.Modules.Booking.Domain;

namespace Odisea.Modules.Booking.Features.Bookings;

// Operator-only shapes. They carry the net cost and the price breakdown and must never be
// returned from an agency endpoint; NetCostLeakGuardTests skips types named "Operator*" for
// exactly this reason, so keep the prefix.

public record BookingListQuery
{
    public BookingStatus? Status { get; init; }
    public Guid? AgencyId { get; init; }

    /// Inclusive check-in date range — operations think in travel dates.
    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 200)]
    public int PageSize { get; init; } = 50;
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public record OperatorBookingSummaryDto(
    Guid Id,
    string Ref,
    string Status,
    Guid AgencyId,
    string ProgramName,
    string HotelName,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Adults,
    int Children,
    decimal? SellAmount,
    decimal PaidAmount,
    decimal BalanceDue,
    string Currency,
    DateTimeOffset? PaymentDueAt,
    DateTimeOffset CreatedAt);

public record OperatorStatusHistoryDto(
    DateTimeOffset At, string From, string To, string? Note, string ActorType, Guid? ActorUserId);

public record OperatorPaymentDto(
    Guid Id,
    decimal Amount,
    string Currency,
    string Method,
    string? Reference,
    DateOnly ReceivedOn,
    Guid RecordedByUserId,
    string? Note,
    DateTimeOffset RecordedAt,
    DateTimeOffset? VoidedAt,
    Guid? VoidedByUserId,
    string? VoidReason);

public record OperatorBookingDto(
    Guid Id,
    string Ref,
    string Status,
    Guid AgencyId,
    Guid AgentUserId,
    Guid ProgramId,
    string ProgramName,
    Guid DepartureId,
    Guid HotelId,
    string HotelName,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Adults,
    int Children,
    string ProviderCode,
    string? ProviderBookingRef,
    decimal? NetAmount,
    decimal? SellAmount,
    decimal? CommissionAmount,
    string Currency,
    /// The PriceBreakdown persisted at re-price time, verbatim (NetCost, Steps, SellPrice, AgencyCommission).
    JsonElement? Breakdown,
    decimal PaidAmount,
    decimal BalanceDue,
    DateTimeOffset? PaymentDueAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PassengerDto> Passengers,
    IReadOnlyList<OperatorPaymentDto> Payments,
    IReadOnlyList<OperatorStatusHistoryDto> History);

public record RecordPaymentRequest(
    [Range(0.01, 1_000_000)] decimal Amount,
    [Required, StringLength(3, MinimumLength = 3)] string Currency,
    PaymentMethod Method,
    DateOnly ReceivedOn,
    [MaxLength(100)] string? Reference = null,
    [MaxLength(500)] string? Note = null);

public record OperatorNoteRequest([Required, MaxLength(500)] string Note);

public record VoidPaymentRequest([Required, MaxLength(300)] string Reason);

public static class OperatorBookingDtoMapping
{
    public static OperatorBookingSummaryDto ToSummaryDto(this Domain.Booking b) => new(
        b.Id, b.Ref, b.Status.ToString(), b.AgencyId, b.ProgramName, b.HotelName,
        b.CheckIn, b.CheckOut, b.Adults, b.Children,
        b.SellAmount, b.PaidAmount, b.BalanceDue, b.Currency, b.PaymentDueAt, b.CreatedAt);

    public static OperatorBookingDto ToOperatorDto(this Domain.Booking b) => new(
        b.Id, b.Ref, b.Status.ToString(), b.AgencyId, b.AgentUserId,
        b.ProgramId, b.ProgramName, b.DepartureId, b.HotelId, b.HotelName,
        b.CheckIn, b.CheckOut, b.Adults, b.Children,
        b.ProviderCode, b.ProviderBookingRef,
        b.NetAmount, b.SellAmount, b.CommissionAmount, b.Currency,
        ParseBreakdown(b.BreakdownJson),
        b.PaidAmount, b.BalanceDue, b.PaymentDueAt, b.CreatedAt,
        [.. b.Passengers.OrderBy(p => p.Position).Select(p => p.ToDto(b.CheckIn))],
        [.. b.Payments.OrderBy(p => p.CreatedAt).Select(p => new OperatorPaymentDto(
            p.Id, p.Amount, p.Currency, p.Method.ToString(), p.Reference, p.ReceivedOn,
            p.RecordedByUserId, p.Note, p.CreatedAt, p.VoidedAt, p.VoidedByUserId, p.VoidReason))],
        [.. b.History.OrderBy(h => h.CreatedAt).Select(h => new OperatorStatusHistoryDto(
            h.CreatedAt, h.FromStatus, h.ToStatus, h.Note, h.ActorType.ToString(), h.ActorUserId))]);

    private static JsonElement? ParseBreakdown(string? json)
    {
        if (json is null)
            return null;

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
