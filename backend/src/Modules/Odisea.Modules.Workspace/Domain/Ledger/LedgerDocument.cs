using Odisea.SharedKernel;

namespace Odisea.Modules.Workspace.Domain.Ledger;

/// A file kept next to a reservation (the operator's contract, a confirmation, a payment proof).
/// The bytes live in the row; lists never select them.
public class LedgerDocument : Entity
{
    public const int MaxBytes = 10 * 1024 * 1024;
    public const int MaxPerReservation = 50;

    public required Guid AgencyId { get; init; }
    public required Guid ReservationId { get; init; }

    /// Set when the file proves one particular payment.
    public Guid? PaymentId { get; init; }
    public required LedgerDocumentType Type { get; init; }
    public required string FileName { get; init; }

    /// Decided from the bytes at upload, never taken from the client.
    public required string ContentType { get; init; }
    public required long SizeBytes { get; init; }
    public required string Sha256 { get; init; }
    public required byte[] Content { get; init; }
    public required Guid UploadedByUserId { get; init; }
}

public enum LedgerDocumentType
{
    OperatorContract,
    Confirmation,
    Invoice,
    PaymentProof,
    Other,
}
