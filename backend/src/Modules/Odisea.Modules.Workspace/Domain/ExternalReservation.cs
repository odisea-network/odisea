using System.Security.Cryptography;
using Odisea.SharedKernel;

namespace Odisea.Modules.Workspace.Domain;

/// A reservation the agency sold outside Odisea (its own offer, its own supplier) and keeps here
/// to track. Odisea knows nothing about its content: the payload is whatever the agency stores,
/// the status is whatever the agency's workflow calls it.
public class ExternalReservation : Entity
{
    public const string DefaultStatus = "New";
    public const int MaxPayloadBytes = 64 * 1024;

    public required Guid AgencyId { get; init; }

    /// The agency's own reference, unique within the agency.
    public required string Reference { get; set; }

    /// Unguessable public handle for the customer-facing tracking page.
    public required string TrackingCode { get; init; }

    public string? Title { get; set; }
    public string Status { get; set; } = DefaultStatus;

    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public DateOnly? TravelFrom { get; set; }
    public DateOnly? TravelTo { get; set; }

    /// Free-form JSON (jsonb), stored verbatim.
    public required string Payload { get; set; }
    public string[] Tags { get; set; } = [];

    public bool IsArchived { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public List<ExternalReservationEvent> Events { get; } = [];

    public ExternalReservationEvent ChangeStatus(string status, string? note, bool isPublic, WorkspaceActor actor, DateTimeOffset now)
    {
        var from = Status;
        Status = status;
        return Record(ExternalEventKind.StatusChanged, actor, now, note, isPublic, from, status);
    }

    public ExternalReservationEvent AddNote(string note, bool isPublic, WorkspaceActor actor, DateTimeOffset now) =>
        Record(ExternalEventKind.Note, actor, now, note, isPublic);

    public ExternalReservationEvent Touch(string? note, WorkspaceActor actor, DateTimeOffset now) =>
        Record(ExternalEventKind.Updated, actor, now, note);

    public ExternalReservationEvent? SetArchived(bool archived, WorkspaceActor actor, DateTimeOffset now)
    {
        if (IsArchived == archived)
            return null;
        IsArchived = archived;
        return Record(archived ? ExternalEventKind.Archived : ExternalEventKind.Restored, actor, now);
    }

    /// Returns the new event: callers persisting an already tracked reservation must Add it to the
    /// context themselves (client-generated ids make EF treat a fixed-up child as Modified).
    public ExternalReservationEvent Record(
        ExternalEventKind kind, WorkspaceActor actor, DateTimeOffset now,
        string? note = null, bool isPublic = false, string? fromStatus = null, string? toStatus = null)
    {
        var evt = new ExternalReservationEvent
        {
            ReservationId = Id,
            Kind = kind,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            IsPublic = isPublic,
            ActorType = actor.Type,
            ActorId = actor.Id,
            CreatedAt = now,
        };
        Events.Add(evt);
        UpdatedAt = now;
        return evt;
    }
}

/// Timeline entry. Public entries (and every status change) appear on the customer tracking page.
public class ExternalReservationEvent : Entity
{
    public required Guid ReservationId { get; init; }
    public required ExternalEventKind Kind { get; init; }
    public string? FromStatus { get; init; }
    public string? ToStatus { get; init; }
    public string? Note { get; init; }
    public bool IsPublic { get; init; }
    public required string ActorType { get; init; }
    public required Guid ActorId { get; init; }
}

public enum ExternalEventKind
{
    Created,
    StatusChanged,
    Note,
    Updated,
    Archived,
    Restored,
}

/// Who acted: an agency user or the agency's API key (machine). Operators never write here.
public sealed record WorkspaceActor(string Type, Guid Id)
{
    public const string Agency = "Agency";
    public const string ApiClient = "ApiClient";
}

public static class TrackingCode
{
    /// No 0/O/1/I so a code read over the phone survives; 32^10 ≈ 10^15 combinations.
    private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
    public const int Length = 10;

    public static string New() => new(RandomNumberGenerator.GetItems<char>(Alphabet, Length));

    public static string? Normalize(string? code)
    {
        var cleaned = code?.Replace("-", "").Replace(" ", "").ToUpperInvariant();
        return cleaned is { Length: Length } && cleaned.All(Alphabet.Contains) ? cleaned : null;
    }
}

public sealed class WorkspaceValidationException(string message) : DomainException(message);

public sealed class WorkspaceNotFoundException(string what) : DomainException($"{what} was not found.");

public sealed class DuplicateReferenceException(string reference)
    : DomainException($"A reservation with reference '{reference}' already exists.");
