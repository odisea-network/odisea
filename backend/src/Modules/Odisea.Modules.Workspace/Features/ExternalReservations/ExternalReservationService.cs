using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Workspace.Domain;
using Odisea.Modules.Workspace.Infrastructure;
using Odisea.SharedKernel;

namespace Odisea.Modules.Workspace.Features.ExternalReservations;

/// Everything is scoped by the caller's agency id (from the token, never from the request).
/// Writes require the ExternalReservations feature; reads keep working after a downgrade so an
/// agency can always get its data out.
public sealed class ExternalReservationService(
    WorkspaceDbContext db,
    IAgencyEntitlements entitlements,
    IAgencyLookup agencies,
    IClock clock,
    ILogger<ExternalReservationService> logger)
{
    private const int MaxTags = 10;
    private const int MaxTagLength = 30;
    private const int MaxPageSize = 200;

    public async Task<ExternalReservationDto> CreateAsync(
        Guid agencyId, WorkspaceActor actor, CreateExternalReservationRequest request, CancellationToken ct)
    {
        await entitlements.RequireAsync(agencyId, AgencyFeature.ExternalReservations, ct);

        var reference = request.Reference.Trim();
        if (reference.Length == 0)
            throw new WorkspaceValidationException("A reference is required.");
        if (await db.ExternalReservations.AnyAsync(r => r.AgencyId == agencyId && r.Reference == reference, ct))
            throw new DuplicateReferenceException(reference);

        var now = clock.UtcNow;
        var reservation = new ExternalReservation
        {
            AgencyId = agencyId,
            Reference = reference,
            TrackingCode = await UniqueTrackingCodeAsync(ct),
            Title = Clean(request.Title),
            Status = CleanStatus(request.Status) ?? ExternalReservation.DefaultStatus,
            Payload = NormalizePayload(request.Payload),
            Tags = NormalizeTags(request.Tags),
            CreatedAt = now,
        };
        ApplyCustomer(reservation, request.Customer);
        ApplyTravel(reservation, request.TravelFrom, request.TravelTo);
        reservation.Record(ExternalEventKind.Created, actor, now, request.Note, toStatus: reservation.Status);

        db.ExternalReservations.Add(reservation);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("External reservation {ReservationId} created for agency {AgencyId} by {ActorType}",
            reservation.Id, agencyId, actor.Type);
        return reservation.ToDto();
    }

    public async Task<PagedResult<ExternalReservationSummaryDto>> ListAsync(
        Guid agencyId, string? status, string? search, DateOnly? travelFrom, DateOnly? travelTo, string? tag,
        bool includeArchived, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.ExternalReservations.AsNoTracking().Where(r => r.AgencyId == agencyId);
        if (!includeArchived)
            query = query.Where(r => !r.IsArchived);
        if (CleanStatus(status)?.ToLowerInvariant() is { } s)
            query = query.Where(r => r.Status.ToLower() == s);
        if (Clean(search)?.ToLowerInvariant() is { } q)
            query = query.Where(r =>
                r.Reference.ToLower().Contains(q)
                || (r.Title != null && r.Title.ToLower().Contains(q))
                || (r.CustomerName != null && r.CustomerName.ToLower().Contains(q))
                || (r.CustomerEmail != null && r.CustomerEmail.ToLower().Contains(q)));
        if (travelFrom is { } from)
            query = query.Where(r => r.TravelFrom >= from);
        if (travelTo is { } to)
            query = query.Where(r => r.TravelFrom <= to);
        if (Clean(tag)?.ToLowerInvariant() is { } t)
            query = query.Where(r => r.Tags.Contains(t));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return new PagedResult<ExternalReservationSummaryDto>(items.Select(r => r.ToSummaryDto()).ToList(), page, pageSize, total);
    }

    public async Task<List<string>> StatusesAsync(Guid agencyId, CancellationToken ct) =>
        await db.ExternalReservations.AsNoTracking()
            .Where(r => r.AgencyId == agencyId)
            .Select(r => r.Status).Distinct().OrderBy(s => s)
            .ToListAsync(ct);

    public async Task<ExternalReservationDto> GetAsync(Guid id, Guid agencyId, CancellationToken ct) =>
        (await LoadAsync(id, agencyId, ct)).ToDto();

    public async Task<ExternalReservationDto> UpdateAsync(
        Guid id, Guid agencyId, WorkspaceActor actor, UpdateExternalReservationRequest request, CancellationToken ct)
    {
        await entitlements.RequireAsync(agencyId, AgencyFeature.ExternalReservations, ct);
        var reservation = await LoadAsync(id, agencyId, ct);
        reservation.Title = Clean(request.Title);
        reservation.Payload = NormalizePayload(request.Payload);
        reservation.Tags = NormalizeTags(request.Tags);
        ApplyCustomer(reservation, request.Customer);
        ApplyTravel(reservation, request.TravelFrom, request.TravelTo);
        db.ExternalReservationEvents.Add(reservation.Touch(request.Note, actor, clock.UtcNow));
        await db.SaveChangesAsync(ct);
        return reservation.ToDto();
    }

    public async Task<ExternalReservationDto> ChangeStatusAsync(
        Guid id, Guid agencyId, WorkspaceActor actor, ChangeStatusRequest request, CancellationToken ct)
    {
        await entitlements.RequireAsync(agencyId, AgencyFeature.ExternalReservations, ct);
        var reservation = await LoadAsync(id, agencyId, ct);
        var status = CleanStatus(request.Status) ?? throw new WorkspaceValidationException("A status is required.");
        db.ExternalReservationEvents.Add(reservation.ChangeStatus(status, request.Note, request.IsPublic, actor, clock.UtcNow));
        await db.SaveChangesAsync(ct);
        return reservation.ToDto();
    }

    public async Task<ExternalReservationDto> AddNoteAsync(
        Guid id, Guid agencyId, WorkspaceActor actor, AddNoteRequest request, CancellationToken ct)
    {
        await entitlements.RequireAsync(agencyId, AgencyFeature.ExternalReservations, ct);
        var reservation = await LoadAsync(id, agencyId, ct);
        if (string.IsNullOrWhiteSpace(request.Note))
            throw new WorkspaceValidationException("A note is required.");
        db.ExternalReservationEvents.Add(reservation.AddNote(request.Note, request.IsPublic, actor, clock.UtcNow));
        await db.SaveChangesAsync(ct);
        return reservation.ToDto();
    }

    public async Task<ExternalReservationDto> SetArchivedAsync(
        Guid id, Guid agencyId, WorkspaceActor actor, bool archived, CancellationToken ct)
    {
        var reservation = await LoadAsync(id, agencyId, ct);
        if (reservation.SetArchived(archived, actor, clock.UtcNow) is { } evt)
            db.ExternalReservationEvents.Add(evt);
        await db.SaveChangesAsync(ct);
        return reservation.ToDto();
    }

    /// Anonymous customer view by tracking code. Works regardless of plan: the customer should not pay for a downgrade.
    public async Task<PublicTrackingDto> TrackAsync(string code, CancellationToken ct)
    {
        var normalized = TrackingCode.Normalize(code) ?? throw new WorkspaceNotFoundException("The reservation");
        var reservation = await db.ExternalReservations.AsNoTracking()
            .Include(r => r.Events)
            .FirstOrDefaultAsync(r => r.TrackingCode == normalized && !r.IsArchived, ct)
            ?? throw new WorkspaceNotFoundException("The reservation");
        var agency = await agencies.FindAsync(reservation.AgencyId, ct);
        return reservation.ToPublicDto(agency?.Name);
    }

    // ----- operator oversight -----

    public async Task<List<WorkspaceUsageDto>> UsageAsync(string? month, CancellationToken ct)
    {
        var (start, end, label) = MonthRange(month);
        var rows = await db.ExternalReservations.AsNoTracking()
            .Where(r => r.CreatedAt >= start && r.CreatedAt < end)
            .GroupBy(r => r.AgencyId)
            .Select(g => new { AgencyId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        return rows.OrderByDescending(r => r.Count).Select(r => new WorkspaceUsageDto(r.AgencyId, label, r.Count)).ToList();
    }

    public async Task<PagedResult<ExternalReservationSummaryDto>> ListForOperatorAsync(
        Guid? agencyId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var query = db.ExternalReservations.AsNoTracking();
        if (agencyId is { } a)
            query = query.Where(r => r.AgencyId == a);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(r => r.UpdatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<ExternalReservationSummaryDto>(items.Select(r => r.ToSummaryDto()).ToList(), page, pageSize, total);
    }

    // ----- helpers -----

    private async Task<ExternalReservation> LoadAsync(Guid id, Guid agencyId, CancellationToken ct) =>
        await db.ExternalReservations
            .Include(r => r.Events)
            .FirstOrDefaultAsync(r => r.Id == id && r.AgencyId == agencyId, ct)
        ?? throw new WorkspaceNotFoundException("The reservation");

    private async Task<string> UniqueTrackingCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = TrackingCode.New();
            if (!await db.ExternalReservations.AnyAsync(r => r.TrackingCode == code, ct))
                return code;
        }
        throw new InvalidOperationException("Could not allocate a unique tracking code.");
    }

    private static void ApplyCustomer(ExternalReservation reservation, CustomerRequest? customer)
    {
        reservation.CustomerName = Clean(customer?.Name);
        reservation.CustomerEmail = Clean(customer?.Email)?.ToLowerInvariant();
        reservation.CustomerPhone = Clean(customer?.Phone);
    }

    private static void ApplyTravel(ExternalReservation reservation, DateOnly? from, DateOnly? to)
    {
        if (from is { } f && to is { } t && t < f)
            throw new WorkspaceValidationException("Travel end date is before the start date.");
        reservation.TravelFrom = from;
        reservation.TravelTo = to;
    }

    public static string NormalizePayload(JsonElement? payload)
    {
        if (payload is not { } element || element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return "{}";
        var raw = element.GetRawText();
        if (Encoding.UTF8.GetByteCount(raw) > ExternalReservation.MaxPayloadBytes)
            throw new WorkspaceValidationException($"The payload exceeds {ExternalReservation.MaxPayloadBytes / 1024} KB.");
        return raw;
    }

    public static string[] NormalizeTags(string[]? tags)
    {
        if (tags is null)
            return [];
        var clean = tags
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => t.Length > 0)
            .Distinct()
            .ToArray();
        if (clean.Length > MaxTags)
            throw new WorkspaceValidationException($"At most {MaxTags} tags are allowed.");
        if (clean.Any(t => t.Length > MaxTagLength))
            throw new WorkspaceValidationException($"Tags are limited to {MaxTagLength} characters.");
        return clean;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? CleanStatus(string? status)
    {
        var clean = Clean(status);
        if (clean is { Length: > 40 })
            throw new WorkspaceValidationException("A status is limited to 40 characters.");
        return clean;
    }

    private (DateTimeOffset Start, DateTimeOffset End, string Label) MonthRange(string? month)
    {
        DateTimeOffset start;
        if (string.IsNullOrWhiteSpace(month))
        {
            var now = clock.UtcNow;
            start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        }
        else if (DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            start = new DateTimeOffset(parsed.Year, parsed.Month, 1, 0, 0, 0, TimeSpan.Zero);
        }
        else
        {
            throw new WorkspaceValidationException("month must be yyyy-MM.");
        }
        return (start, start.AddMonths(1), start.ToString("yyyy-MM", CultureInfo.InvariantCulture));
    }
}
