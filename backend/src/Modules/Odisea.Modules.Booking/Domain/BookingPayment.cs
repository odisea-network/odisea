using Odisea.SharedKernel;

namespace Odisea.Modules.Booking.Domain;

/// Money the operator has received for a reservation. Financial records are never deleted:
/// a mistaken entry is voided, which keeps the audit trail and excludes it from the paid total.
public class BookingPayment : Entity
{
    public required Guid BookingId { get; set; }
    public required decimal Amount { get; set; }
    public required string Currency { get; set; }
    public required PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public required DateOnly ReceivedOn { get; set; }
    public required Guid RecordedByUserId { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset? VoidedAt { get; set; }
    public Guid? VoidedByUserId { get; set; }
    public string? VoidReason { get; set; }

    public bool IsVoided => VoidedAt is not null;
}

public enum PaymentMethod
{
    BankTransfer,
    Card,
    Cash,
    Other,
}
