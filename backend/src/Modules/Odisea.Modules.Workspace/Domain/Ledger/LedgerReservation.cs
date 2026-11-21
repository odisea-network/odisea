using Odisea.SharedKernel;

namespace Odisea.Modules.Workspace.Domain.Ledger;

/// A booking the agency made with a tour operator, kept for operational follow-up: what is owed to the
/// operator, when, what was actually paid and what commission is expected. Not an accounting record.
public class LedgerReservation : Entity
{
    public required Guid AgencyId { get; init; }
    public required Guid TourOperatorId { get; set; }

    /// The operator's booking number; unique per agency and operator.
    public required string Reference { get; set; }
    public required string HolderName { get; set; }
    public DateOnly BookedOn { get; set; }
    public DateOnly TravelStart { get; set; }
    public DateOnly? TravelEnd { get; set; }

    /// What the agency owes the operator for this booking.
    public decimal OperatorTotal { get; set; }
    public required string Currency { get; set; }

    /// What the traveller pays the agency. Informational; never used for the operator balance.
    public decimal? CustomerSaleAmount { get; set; }

    public LedgerReservationStatus Status { get; set; } = LedgerReservationStatus.Confirmed;
    public string? Notes { get; set; }
    public string? CancellationNote { get; set; }

    public CommissionType? CommissionType { get; set; }
    public decimal? CommissionRate { get; set; }
    public CommissionBase? CommissionBase { get; set; }

    /// Expected, not earned: it becomes income only when the agency records it as received.
    public decimal? CommissionExpected { get; set; }
    public decimal CommissionReceived { get; set; }

    public required Guid CreatedByUserId { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// Postgres xmin. Payments and schedule rows touch the reservation row so that two people working
    /// from the same stale figures cannot both save.
    public uint Version { get; set; }

    public List<LedgerInstallment> Installments { get; } = [];
    public List<LedgerPayment> Payments { get; } = [];

    public decimal PaidTotal => Payments.Where(p => !p.IsVoided).Sum(p => p.Amount);
    public bool HasPayments => Payments.Any(p => !p.IsVoided);
}

public enum LedgerReservationStatus
{
    Draft,
    Confirmed,
    Cancelled,
    Completed,
}

public enum CommissionType
{
    Percent,
    Fixed,
}

/// What a percentage commission is taken from. Manual means the agency typed the expected amount itself.
public enum CommissionBase
{
    OperatorTotal,
    CustomerSale,
    Manual,
}

/// A planned payment to the operator. Planned only: money moves when a LedgerPayment is recorded.
public class LedgerInstallment : Entity
{
    public required Guid ReservationId { get; init; }
    public required Guid AgencyId { get; init; }
    public LedgerInstallmentLabel Label { get; init; }
    public decimal Amount { get; init; }
    public DateOnly DueDate { get; init; }
    public int SortOrder { get; init; }
}

public enum LedgerInstallmentLabel
{
    Deposit,
    Balance,
    Other,
}

/// Money actually sent to the operator. Never deleted: a mistaken entry is voided and stays visible.
public class LedgerPayment : Entity
{
    public required Guid ReservationId { get; init; }
    public required Guid AgencyId { get; init; }
    public decimal Amount { get; init; }
    public required string Currency { get; init; }
    public DateOnly PaidOn { get; init; }
    public LedgerPaymentMethod Method { get; init; }
    public string? Reference { get; init; }
    public string? Note { get; init; }

    /// Chosen by the client; a retried request with the same key records nothing twice.
    public Guid? IdempotencyKey { get; init; }
    public required Guid RecordedByUserId { get; init; }

    public DateTimeOffset? VoidedAt { get; set; }
    public Guid? VoidedByUserId { get; set; }
    public string? VoidReason { get; set; }
    public bool IsVoided => VoidedAt is not null;
}

public enum LedgerPaymentMethod
{
    BankTransfer,
    Card,
    Cash,
    Other,
}

/// Who changed which money-relevant value, and why. Written in the same SaveChanges as the change.
public class LedgerAuditEvent : Entity
{
    public required Guid AgencyId { get; init; }
    public Guid? ReservationId { get; init; }
    public required string EntityType { get; init; }
    public required Guid EntityId { get; init; }
    public required string Action { get; init; }
    public required string ActorType { get; init; }
    public required Guid ActorId { get; init; }
    public string? Reason { get; init; }

    /// JSON object: field name → { from, to }. Free-text fields record only that they changed.
    public required string Changes { get; init; }
}

/// The caller. IsAdmin is true only for a signed-in agency admin — never for an API key.
public sealed record LedgerActor(Guid Id, string Type, bool IsAdmin);

/// Rejected input. Code is stable and machine-readable so clients can show their own wording.
public sealed class LedgerValidationException(string code, string message) : DomainException(message)
{
    public string Code { get; } = code;
}

public sealed class LedgerNotFoundException() : DomainException("Not found.");

public sealed class LedgerConflictException(string code, string message) : DomainException(message)
{
    public string Code { get; } = code;
}

/// The action is reserved for agency admins (corrections to recorded money).
public sealed class LedgerAdminRequiredException(string message) : DomainException(message);
