using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Odisea.Modules.Workspace.Domain;

namespace Odisea.Modules.Workspace.Features.ExternalReservations;

public sealed record CustomerRequest(
    [MaxLength(200)] string? Name,
    [EmailAddress, MaxLength(320)] string? Email,
    [MaxLength(30)] string? Phone);

// Payload: any JSON the agency wants to keep (object, array, scalar), up to 64 KB.
public sealed record CreateExternalReservationRequest(
    [Required, MaxLength(50)] string Reference,
    [MaxLength(200)] string? Title,
    [MaxLength(40)] string? Status,
    CustomerRequest? Customer,
    DateOnly? TravelFrom,
    DateOnly? TravelTo,
    string[]? Tags,
    JsonElement? Payload,
    [MaxLength(2000)] string? Note);

/// Replaces every mutable field; status and archive state have their own actions.
public sealed record UpdateExternalReservationRequest(
    [MaxLength(200)] string? Title,
    CustomerRequest? Customer,
    DateOnly? TravelFrom,
    DateOnly? TravelTo,
    string[]? Tags,
    JsonElement? Payload,
    [MaxLength(2000)] string? Note);

public sealed record ChangeStatusRequest(
    [Required, MaxLength(40)] string Status,
    [MaxLength(2000)] string? Note,
    bool IsPublic = false);

public sealed record AddNoteRequest(
    [Required, MaxLength(2000)] string Note,
    bool IsPublic = false);

public sealed record CustomerDto(string? Name, string? Email, string? Phone);

public sealed record ExternalEventDto(
    Guid Id,
    ExternalEventKind Kind,
    string? FromStatus,
    string? ToStatus,
    string? Note,
    bool IsPublic,
    string ActorType,
    Guid ActorId,
    DateTimeOffset At);

public sealed record ExternalReservationSummaryDto(
    Guid Id,
    string Reference,
    string TrackingCode,
    string? Title,
    string Status,
    CustomerDto Customer,
    DateOnly? TravelFrom,
    DateOnly? TravelTo,
    string[] Tags,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ExternalReservationDto(
    Guid Id,
    string Reference,
    string TrackingCode,
    string? Title,
    string Status,
    CustomerDto Customer,
    DateOnly? TravelFrom,
    DateOnly? TravelTo,
    string[] Tags,
    JsonElement Payload,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    List<ExternalEventDto> Events);

public sealed record PagedResult<T>(List<T> Items, int Page, int PageSize, int TotalCount);

/// What the agency's customer sees: no contact data, no payload, no actor ids.
public sealed record PublicTrackingDto(
    string Reference,
    string? Title,
    string Status,
    string? AgencyName,
    DateOnly? TravelFrom,
    DateOnly? TravelTo,
    DateTimeOffset UpdatedAt,
    List<PublicEventDto> Events);

public sealed record PublicEventDto(ExternalEventKind Kind, string? ToStatus, string? Note, DateTimeOffset At);

public static class ExternalReservationMapping
{
    public static ExternalReservationSummaryDto ToSummaryDto(this ExternalReservation r) => new(
        r.Id, r.Reference, r.TrackingCode, r.Title, r.Status,
        new CustomerDto(r.CustomerName, r.CustomerEmail, r.CustomerPhone),
        r.TravelFrom, r.TravelTo, r.Tags, r.IsArchived, r.CreatedAt, r.UpdatedAt);

    public static ExternalReservationDto ToDto(this ExternalReservation r) => new(
        r.Id, r.Reference, r.TrackingCode, r.Title, r.Status,
        new CustomerDto(r.CustomerName, r.CustomerEmail, r.CustomerPhone),
        r.TravelFrom, r.TravelTo, r.Tags, JsonDocument.Parse(r.Payload).RootElement.Clone(),
        r.IsArchived, r.CreatedAt, r.UpdatedAt,
        r.Events.OrderBy(e => e.CreatedAt).Select(e => e.ToDto()).ToList());

    public static ExternalEventDto ToDto(this ExternalReservationEvent e) => new(
        e.Id, e.Kind, e.FromStatus, e.ToStatus, e.Note, e.IsPublic, e.ActorType, e.ActorId, e.CreatedAt);

    /// Status changes always show (the status is public anyway); their notes and plain notes only when public.
    public static PublicTrackingDto ToPublicDto(this ExternalReservation r, string? agencyName) => new(
        r.Reference, r.Title, r.Status, agencyName, r.TravelFrom, r.TravelTo, r.UpdatedAt,
        r.Events
            .Where(e => e.Kind is ExternalEventKind.Created or ExternalEventKind.StatusChanged || (e.Kind == ExternalEventKind.Note && e.IsPublic))
            .OrderBy(e => e.CreatedAt)
            .Select(e => new PublicEventDto(e.Kind, e.ToStatus, e.IsPublic ? e.Note : null, e.CreatedAt))
            .ToList());
}
