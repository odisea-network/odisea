using Odisea.SharedKernel;

namespace Odisea.Modules.Booking.Domain;

public class Booking : Entity
{
    private static readonly Dictionary<BookingStatus, BookingStatus[]> AllowedTransitions = new()
    {
        [BookingStatus.Draft] = [BookingStatus.PriceConfirmed, BookingStatus.Cancelled],
        [BookingStatus.PriceConfirmed] =
            [BookingStatus.PriceConfirmed, BookingStatus.PendingConfirmation, BookingStatus.Cancelled],
        // Cancelled from PendingConfirmation is the operator's rescue path after a crash mid provider call.
        [BookingStatus.PendingConfirmation] =
            [BookingStatus.AwaitingPayment, BookingStatus.Failed, BookingStatus.Cancelled],
        [BookingStatus.AwaitingPayment] =
            [BookingStatus.Confirmed, BookingStatus.Rejected, BookingStatus.Expired, BookingStatus.Cancelled],
        [BookingStatus.Confirmed] = [BookingStatus.Cancelled],
        [BookingStatus.Rejected] = [],
        [BookingStatus.Expired] = [],
        [BookingStatus.Cancelled] = [],
        [BookingStatus.Failed] = [],
    };

    public required string Ref { get; set; }
    public required Guid AgencyId { get; set; }

    /// The `sub` that created the draft: an agency user, or an API key once partners book directly.
    public required Guid AgentUserId { get; set; }

    // Occupancy the offer was searched and priced for (from the protected offer envelope,
    // never from the client). The passenger list must match it before the provider is booked.
    public required int Adults { get; set; }
    public required int Children { get; set; }

    // Snapshots of the booked product — names survive later catalog edits.
    public required Guid ProgramId { get; set; }
    public required string ProgramName { get; set; }
    public required Guid DepartureId { get; set; }
    public required Guid HotelId { get; set; }
    public required string HotelName { get; set; }
    public required DateOnly CheckIn { get; set; }
    public required DateOnly CheckOut { get; set; }

    public required string ProviderCode { get; set; }

    /// The RAW provider token (internal only — it may embed the net cost).
    /// Clients only ever see the DataProtection-wrapped envelope.
    public required string OfferToken { get; set; }

    public string? ProviderBookingRef { get; set; }

    public BookingStatus Status { get; private set; } = BookingStatus.Draft;

    /// Set when the provider holds the reservation; the operator must have the money by then.
    public DateTimeOffset? PaymentDueAt { get; set; }

    /// Optimistic concurrency token mapped to Postgres xmin: two operators cannot both act on
    /// the same stale row (second SaveChanges fails with DbUpdateConcurrencyException → 409).
    public uint Version { get; set; }

    public decimal? NetAmount { get; set; }
    public decimal? SellAmount { get; set; }
    public decimal? CommissionAmount { get; set; }
    public string Currency { get; set; } = "EUR";

    /// Full PriceBreakdown JSON — the price stays explainable forever.
    public string? BreakdownJson { get; set; }

    public List<BookingPassenger> Passengers { get; } = [];
    public List<BookingPayment> Payments { get; } = [];
    public List<BookingStatusHistory> History { get; } = [];

    public decimal PaidAmount => Payments.Where(p => !p.IsVoided).Sum(p => p.Amount);
    public decimal BalanceDue => (SellAmount ?? 0m) - PaidAmount;
    public bool IsFullyPaid => SellAmount is { } sell && PaidAmount >= sell;

    public void TransitionTo(BookingStatus next, BookingActor actor, string? note, DateTimeOffset now)
    {
        if (!AllowedTransitions[Status].Contains(next))
            throw new InvalidBookingTransitionException(Status, next);

        History.Add(new BookingStatusHistory
        {
            BookingId = Id,
            FromStatus = Status.ToString(),
            ToStatus = next.ToString(),
            ActorType = actor.Type,
            ActorUserId = actor.UserId,
            Note = note,
            CreatedAt = now,
        });
        Status = next;
    }

    /// Everything the provider and the operator need before the reservation is submitted.
    /// Problems are collected and reported together; passengers are identified by position
    /// only — never by name or document number, because this message reaches clients and logs.
    public void EnsureReadyForSubmit()
    {
        List<string> problems = [];

        if (SellAmount is null)
            problems.Add("The price has not been confirmed.");

        if (Passengers.Count != Adults + Children)
            problems.Add($"{Passengers.Count} passenger(s) for an occupancy of {Adults} adult(s) and {Children} child(ren).");

        var leads = Passengers.Count(p => p.IsLead);
        if (leads != 1)
            problems.Add(leads == 0 ? "No lead passenger." : "More than one lead passenger.");

        var adults = 0;
        var minors = 0;
        foreach (var p in Passengers.OrderBy(p => p.Position))
        {
            var label = $"Passenger {p.Position + 1}";
            List<string> missing = [];

            if (p.Gender is null) missing.Add("gender");
            if (p.BirthDate is null) missing.Add("birth date");
            else if (p.BirthDate > CheckIn) problems.Add($"{label}: birth date is after check-in.");
            if (p.Nationality is null) missing.Add("nationality");
            if (p.DocumentType is null) missing.Add("document type");
            if (p.DocumentNumber is null) missing.Add("document number");
            if (p.DocumentIssuingCountry is null) missing.Add("document issuing country");
            if (p.DocumentExpiryDate is null) missing.Add("document expiry date");
            else if (p.DocumentExpiryDate < CheckOut) problems.Add($"{label}: travel document expires before check-out.");
            if (p.IsLead)
            {
                if (string.IsNullOrWhiteSpace(p.Email)) missing.Add("email (lead)");
                if (string.IsNullOrWhiteSpace(p.Phone)) missing.Add("phone (lead)");
            }

            if (missing.Count > 0)
                problems.Add($"{label}: missing {string.Join(", ", missing)}.");

            if (p.TypeAt(CheckIn) is { } type)
            {
                if (type == PassengerType.Adult) adults++;
                else minors++;
            }
        }

        if (problems.Count == 0 && (adults != Adults || minors != Children))
            problems.Add(
                $"Passenger ages give {adults} adult(s) and {minors} child(ren), " +
                $"but the offer is for {Adults} adult(s) and {Children} child(ren).");

        if (problems.Count > 0)
            throw new BookingValidationException(string.Join(" ", problems));
    }
}

public enum BookingStatus
{
    Draft,
    PriceConfirmed,
    PendingConfirmation,
    AwaitingPayment,
    Confirmed,
    Rejected,
    Expired,
    Cancelled,
    Failed,
}

public sealed class InvalidBookingTransitionException(BookingStatus from, BookingStatus to)
    : DomainException($"A booking cannot move from {from} to {to}.");

public sealed class BookingValidationException(string message) : DomainException(message);

/// Not found OR not owned by the caller's agency — the two are deliberately indistinguishable.
public sealed class BookingNotFoundException() : DomainException("Booking not found.");

public sealed class PaymentNotFoundException() : DomainException("Payment not found on this booking.");

/// The requested action is not available in the booking's current state.
public sealed class BookingStateException : DomainException
{
    public BookingStateException(BookingStatus status, string action)
        : base($"Cannot {action} a booking in status {status}.") { }

    public BookingStateException(string message) : base(message) { }
}

public sealed class PaymentIncompleteException(decimal balanceDue, string currency)
    : DomainException($"The reservation is not fully paid. Balance due: {balanceDue:0.00} {currency}.");

public sealed class OfferUnavailableException()
    : DomainException("The offer is no longer available at the provider.");
