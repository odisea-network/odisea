using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Booking.Domain;
using Odisea.Modules.Booking.Infrastructure;
using Odisea.Modules.Integrations.PublicApi;
using Odisea.SharedKernel;

namespace Odisea.Modules.Booking.Features.Bookings;

/// The operator side of a reservation: oversight across agencies, payments, and the
/// decisions an agency cannot take itself (confirm, reject, expire, cancel after payment).
public class OperatorBookingService(BookingDbContext db, IProviderRegistry providers, IClock clock)
{
    public async Task<PagedResult<OperatorBookingSummaryDto>> ListAsync(BookingListQuery query, CancellationToken ct)
    {
        var bookings = db.Bookings.Include(b => b.Payments).AsNoTracking().AsQueryable();
        if (query.Status is { } status)
            bookings = bookings.Where(b => b.Status == status);
        if (query.AgencyId is { } agencyId)
            bookings = bookings.Where(b => b.AgencyId == agencyId);
        if (query.From is { } from)
            bookings = bookings.Where(b => b.CheckIn >= from);
        if (query.To is { } to)
            bookings = bookings.Where(b => b.CheckIn <= to);

        var total = await bookings.CountAsync(ct);
        var items = await bookings
            .OrderByDescending(b => b.CreatedAt)
            .ThenBy(b => b.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<OperatorBookingSummaryDto>(
            [.. items.Select(b => b.ToSummaryDto())], query.Page, query.PageSize, total);
    }

    public async Task<OperatorBookingDto?> GetAsync(Guid bookingId, CancellationToken ct)
    {
        var booking = await db.Bookings
            .Include(b => b.Passengers)
            .Include(b => b.Payments)
            .Include(b => b.History)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct);
        return booking?.ToOperatorDto();
    }

    /// The commercial confirmation: only once the money is in. The provider already holds
    /// the reservation, so nothing is sent outward here.
    public async Task<OperatorBookingDto> ConfirmAsync(Guid bookingId, BookingActor actor, CancellationToken ct)
    {
        var booking = await LoadAsync(bookingId, ct);
        if (booking.Status != BookingStatus.AwaitingPayment)
            throw new InvalidBookingTransitionException(booking.Status, BookingStatus.Confirmed);
        if (!booking.IsFullyPaid)
            throw new PaymentIncompleteException(booking.BalanceDue, booking.Currency);

        Transition(booking, BookingStatus.Confirmed, actor,
            $"Confirmed by the operator; paid {booking.PaidAmount:0.00} {booking.Currency}");
        await db.SaveChangesAsync(ct);
        return booking.ToOperatorDto();
    }

    public async Task<OperatorBookingDto> RejectAsync(
        Guid bookingId, string note, BookingActor actor, CancellationToken ct)
    {
        var booking = await LoadAsync(bookingId, ct);
        if (booking.Status != BookingStatus.AwaitingPayment)
            throw new InvalidBookingTransitionException(booking.Status, BookingStatus.Rejected);

        await ReleaseAtProviderAsync(booking, ct);
        Transition(booking, BookingStatus.Rejected, actor, note);
        await db.SaveChangesAsync(ct);
        return booking.ToOperatorDto();
    }

    /// Manual for the MVP; a scheduled job can call the same path later.
    public async Task<OperatorBookingDto> ExpireAsync(Guid bookingId, BookingActor actor, CancellationToken ct)
    {
        var booking = await LoadAsync(bookingId, ct);
        if (booking.Status != BookingStatus.AwaitingPayment)
            throw new InvalidBookingTransitionException(booking.Status, BookingStatus.Expired);
        if (booking.PaymentDueAt is not { } dueAt || dueAt > clock.UtcNow)
            throw new BookingStateException("The payment deadline has not passed yet.");

        await ReleaseAtProviderAsync(booking, ct);
        Transition(booking, BookingStatus.Expired, actor, $"Payment deadline {dueAt:yyyy-MM-dd HH:mm} UTC passed");
        await db.SaveChangesAsync(ct);
        return booking.ToOperatorDto();
    }

    public async Task<OperatorBookingDto> CancelAsync(
        Guid bookingId, string note, BookingActor actor, CancellationToken ct)
    {
        var booking = await LoadAsync(bookingId, ct);
        if (booking.Status is not (BookingStatus.PendingConfirmation
            or BookingStatus.AwaitingPayment or BookingStatus.Confirmed))
            throw new InvalidBookingTransitionException(booking.Status, BookingStatus.Cancelled);

        await ReleaseAtProviderAsync(booking, ct);
        Transition(booking, BookingStatus.Cancelled, actor, note);
        await db.SaveChangesAsync(ct);
        return booking.ToOperatorDto();
    }

    public async Task<OperatorBookingDto> RecordPaymentAsync(
        Guid bookingId, RecordPaymentRequest request, BookingActor actor, CancellationToken ct)
    {
        var recordedBy = actor.UserId
            ?? throw new ArgumentException("Operator actors always carry the caller's id.", nameof(actor));

        var booking = await LoadAsync(bookingId, ct);
        if (booking.Status != BookingStatus.AwaitingPayment)
            throw new BookingStateException(booking.Status, "record a payment for");

        var currency = request.Currency.ToUpperInvariant();
        if (currency != booking.Currency)
            throw new BookingValidationException(
                $"Payment currency {currency} does not match the booking currency {booking.Currency}.");
        if (request.Amount > booking.BalanceDue)
            throw new BookingValidationException(
                $"Payment of {request.Amount:0.00} {currency} exceeds the balance due of {booking.BalanceDue:0.00} {currency}.");

        var now = clock.UtcNow;
        if (request.ReceivedOn > DateOnly.FromDateTime(now.UtcDateTime))
            throw new BookingValidationException("A payment cannot be received in the future.");

        var payment = new BookingPayment
        {
            BookingId = booking.Id,
            Amount = request.Amount,
            Currency = currency,
            Method = request.Method,
            Reference = request.Reference?.Trim(),
            ReceivedOn = request.ReceivedOn,
            RecordedByUserId = recordedBy,
            Note = request.Note?.Trim(),
            CreatedAt = now,
        };
        booking.Payments.Add(payment);
        // Preset v7 ids would make change-tracker discovery treat the row as Modified.
        db.Add(payment);
        GuardBalanceWithConcurrencyToken(booking);

        await db.SaveChangesAsync(ct);
        return booking.ToOperatorDto();
    }

    public async Task<OperatorBookingDto> VoidPaymentAsync(
        Guid bookingId, Guid paymentId, string reason, BookingActor actor, CancellationToken ct)
    {
        var booking = await LoadAsync(bookingId, ct);
        if (booking.Status != BookingStatus.AwaitingPayment)
            throw new BookingStateException(booking.Status, "void a payment of");

        var payment = booking.Payments.FirstOrDefault(p => p.Id == paymentId)
            ?? throw new PaymentNotFoundException();
        if (payment.IsVoided)
            throw new BookingValidationException("The payment is already voided.");

        payment.VoidedAt = clock.UtcNow;
        payment.VoidedByUserId = actor.UserId;
        payment.VoidReason = reason.Trim();
        GuardBalanceWithConcurrencyToken(booking);

        await db.SaveChangesAsync(ct);
        return booking.ToOperatorDto();
    }

    /// The balance check above ran against the payments loaded with the booking. Payment rows
    /// alone would not touch bookings.xmin, so two simultaneous operators could both pass it and
    /// overpay. Marking the booking modified makes SaveChanges include the row — and therefore
    /// its concurrency token — so the loser gets DbUpdateConcurrencyException (409) instead.
    private void GuardBalanceWithConcurrencyToken(Domain.Booking booking) =>
        db.Entry(booking).State = EntityState.Modified;

    /// Gives the inventory back when the provider is holding it; a refusal is surfaced, not hidden.
    private async Task ReleaseAtProviderAsync(Domain.Booking booking, CancellationToken ct)
    {
        if (booking.ProviderBookingRef is not { } providerRef)
            return;

        var result = await providers.Get(booking.ProviderCode).CancelAsync(providerRef, ct);
        if (!result.Success)
            throw new BookingValidationException($"The provider refused the cancellation: {result.FailureReason}");
    }

    private void Transition(Domain.Booking booking, BookingStatus next, BookingActor actor, string? note)
    {
        booking.TransitionTo(next, actor, note, clock.UtcNow);
        db.Add(booking.History[^1]);
    }

    private async Task<Domain.Booking> LoadAsync(Guid bookingId, CancellationToken ct) =>
        await db.Bookings
            .Include(b => b.Passengers)
            .Include(b => b.Payments)
            .Include(b => b.History)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct)
        ?? throw new BookingNotFoundException();
}
