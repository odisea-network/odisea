using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Workspace.Domain.Ledger;
using Odisea.Modules.Workspace.Infrastructure;
using Odisea.SharedKernel;

namespace Odisea.Modules.Workspace.Features.Ledger;

/// Files attached to a ledger reservation. Scoped by the caller's agency like the rest of the ledger:
/// a document of another agency, or one under a different reservation, is simply not found.
public sealed class LedgerDocumentService(WorkspaceDbContext db, IAgencyEntitlements entitlements, IClock clock)
{
    public async Task<List<LedgerDocumentDto>> ListAsync(Guid reservationId, Guid agencyId, CancellationToken ct)
    {
        await EnsureReservationAsync(reservationId, agencyId, ct);
        return await db.LedgerDocuments.AsNoTracking()
            .Where(d => d.AgencyId == agencyId && d.ReservationId == reservationId)
            .OrderByDescending(d => d.CreatedAt).ThenBy(d => d.Id)
            .Select(d => new LedgerDocumentDto(
                d.Id, d.PaymentId, d.Type, d.FileName, d.ContentType, d.SizeBytes, d.UploadedByUserId, d.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<LedgerDocumentDto> UploadAsync(
        Guid reservationId, Guid agencyId, LedgerActor actor, byte[] bytes, string? fileName,
        LedgerDocumentType type, Guid? paymentId, CancellationToken ct)
    {
        await entitlements.RequireAsync(agencyId, AgencyFeature.ReservationLedger, ct);
        await EnsureReservationAsync(reservationId, agencyId, ct);

        var contentType = LedgerFile.Inspect(bytes);
        if (paymentId is { } pid && !await db.LedgerPayments.AnyAsync(
                p => p.Id == pid && p.ReservationId == reservationId && p.AgencyId == agencyId, ct))
            throw new LedgerValidationException("payment_unknown", "The payment does not belong to this reservation.");

        var existing = db.LedgerDocuments.Where(d => d.AgencyId == agencyId && d.ReservationId == reservationId);
        if (await existing.CountAsync(ct) >= LedgerDocument.MaxPerReservation)
            throw new LedgerValidationException("too_many_documents",
                $"A reservation may hold at most {LedgerDocument.MaxPerReservation} documents.");
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (await existing.AnyAsync(d => d.Sha256 == sha256, ct))
            throw new LedgerConflictException("document_duplicate", "This file is already attached to the reservation.");

        var document = new LedgerDocument
        {
            AgencyId = agencyId,
            ReservationId = reservationId,
            PaymentId = paymentId,
            Type = type,
            FileName = LedgerFile.SafeName(fileName, contentType),
            ContentType = contentType,
            SizeBytes = bytes.LongLength,
            Sha256 = sha256,
            Content = bytes,
            UploadedByUserId = actor.Id,
            CreatedAt = clock.UtcNow,
        };
        db.LedgerDocuments.Add(document);
        Audit(document, "DocumentUploaded", actor);
        await db.SaveChangesAsync(ct);

        return new LedgerDocumentDto(document.Id, document.PaymentId, document.Type, document.FileName,
            document.ContentType, document.SizeBytes, document.UploadedByUserId, document.CreatedAt);
    }

    public async Task<LedgerDocumentFile> GetFileAsync(Guid reservationId, Guid documentId, Guid agencyId, CancellationToken ct) =>
        await db.LedgerDocuments.AsNoTracking()
            .Where(d => d.Id == documentId && d.ReservationId == reservationId && d.AgencyId == agencyId)
            .Select(d => new LedgerDocumentFile(d.Content, d.ContentType, d.FileName))
            .FirstOrDefaultAsync(ct)
        ?? throw new LedgerNotFoundException();

    /// The row goes, the audit event stays: who removed which file (name and hash) remains on record.
    public async Task DeleteAsync(Guid reservationId, Guid documentId, Guid agencyId, LedgerActor actor, CancellationToken ct)
    {
        await entitlements.RequireAsync(agencyId, AgencyFeature.ReservationLedger, ct);

        var document = await db.LedgerDocuments
                           .FirstOrDefaultAsync(d => d.Id == documentId && d.ReservationId == reservationId && d.AgencyId == agencyId, ct)
                       ?? throw new LedgerNotFoundException();
        if (!actor.IsAdmin)
            throw new LedgerAdminRequiredException("Only an agency admin can delete a document.");

        db.LedgerDocuments.Remove(document);
        Audit(document, "DocumentDeleted", actor);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureReservationAsync(Guid reservationId, Guid agencyId, CancellationToken ct)
    {
        if (!await db.LedgerReservations.AnyAsync(r => r.Id == reservationId && r.AgencyId == agencyId, ct))
            throw new LedgerNotFoundException();
    }

    private void Audit(LedgerDocument document, string action, LedgerActor actor) =>
        db.LedgerAuditEvents.Add(new LedgerAuditEvent
        {
            AgencyId = document.AgencyId,
            ReservationId = document.ReservationId,
            EntityType = "Document",
            EntityId = document.Id,
            Action = action,
            ActorType = actor.Type,
            ActorId = actor.Id,
            Changes = new LedgerChanges()
                .Set("type", document.Type)
                .Set("fileName", document.FileName)
                .Set("sizeBytes", document.SizeBytes)
                .Set("sha256", document.Sha256)
                .ToJson(),
            CreatedAt = clock.UtcNow,
        });
}
