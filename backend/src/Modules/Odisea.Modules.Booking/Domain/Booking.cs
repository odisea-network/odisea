using Odisea.SharedKernel;

namespace Odisea.Modules.Booking.Domain;

public class Booking : Entity
{
    private static readonly Dictionary<BookingStatus, BookingStatus[]> AllowedTransitions = new()
    {
        [BookingStatus.Draft] = [BookingStatus.PriceConfirmed, BookingStatus.Cancelled],
        [BookingStatus.PriceConfirmed] =
            [BookingStatus.PriceConfirmed, BookingStatus.PendingConfirmation, BookingStatus.Cancelled],
        [BookingStatus.PendingConfirmation] = [BookingStatus.Confirmed, BookingStatus.Failed],
        [BookingStatus.Confirmed] = [BookingStatus.Cancelled],
        [BookingStatus.Cancelled] = [],
        [BookingStatus.Failed] = [],
    };

    public required string Ref { get; set; }
    public required Guid AgencyId { get; set; }
    public required Guid AgentUserId { get; set; }

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
    /// Clients only ever see the DataProtection-wrapped version.
    public required string OfferToken { get; set; }

    public string? ProviderBookingRef { get; set; }

    public BookingStatus Status { get; private set; } = BookingStatus.Draft;

    public decimal? NetAmount { get; set; }
    public decimal? SellAmount { get; set; }
    public decimal? CommissionAmount { get; set; }
    public string Currency { get; set; } = "EUR";

    /// Full PriceBreakdown JSON — the price stays explainable forever.
    public string? BreakdownJson { get; set; }

    public List<BookingPassenger> Passengers { get; } = [];
    public List<BookingStatusHistory> History { get; } = [];

    public void TransitionTo(BookingStatus next, string? note, DateTimeOffset now)
    {
        if (!AllowedTransitions[Status].Contains(next))
            throw new InvalidBookingTransitionException(Status, next);

        History.Add(new BookingStatusHistory
        {
            BookingId = Id,
            FromStatus = Status.ToString(),
            ToStatus = next.ToString(),
            Note = note,
            CreatedAt = now,
        });
        Status = next;
    }
}

public enum BookingStatus
{
    Draft,
    PriceConfirmed,
    PendingConfirmation,
    Confirmed,
    Cancelled,
    Failed,
}

public sealed class InvalidBookingTransitionException(BookingStatus from, BookingStatus to)
    : DomainException($"A booking cannot move from {from} to {to}.");

public sealed class BookingValidationException(string message) : DomainException(message);

public sealed class OfferUnavailableException()
    : DomainException("The offer is no longer available at the provider.");
