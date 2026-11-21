using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Workspace.Domain.Ledger;
using Odisea.Modules.Workspace.Features.Ledger;
using Odisea.SharedKernel;

namespace Odisea.Modules.Workspace.Controllers;

/// Files kept with a ledger reservation. Downloads need the same token as everything else; there is
/// no public link to a document.
[ApiController]
[Route("api/v1/agency/ledger/reservations/{reservationId:guid}/documents")]
[Authorize(Policy = AuthPolicies.Agency)]
public class AgencyLedgerDocumentsController(LedgerDocumentService documents, ICurrentUser currentUser)
    : LedgerControllerBase(currentUser)
{
    private const long UploadLimit = LedgerDocument.MaxBytes + 64 * 1024;

    [HttpGet]
    public Task<IActionResult> List(Guid reservationId, CancellationToken ct) =>
        Execute(async (agencyId, _) => Ok(await documents.ListAsync(reservationId, agencyId, ct)));

    [HttpPost]
    [RequestSizeLimit(UploadLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimit)]
    public Task<IActionResult> Upload(
        Guid reservationId,
        IFormFile? file,
        [FromForm] LedgerDocumentType? type,
        [FromForm] Guid? paymentId,
        CancellationToken ct) =>
        Execute(async (agencyId, actor) =>
        {
            var bytes = await ReadAsync(file, ct);
            var dto = await documents.UploadAsync(
                reservationId, agencyId, actor, bytes, file?.FileName, type ?? LedgerDocumentType.Other, paymentId, ct);
            return CreatedAtAction(nameof(GetFile), new { reservationId, documentId = dto.Id }, dto);
        });

    [HttpGet("{documentId:guid}/file")]
    public Task<IActionResult> GetFile(Guid reservationId, Guid documentId, CancellationToken ct) =>
        Execute(async (agencyId, _) =>
        {
            var file = await documents.GetFileAsync(reservationId, documentId, agencyId, ct);
            // Always a download, never rendered in the portal's origin.
            Response.Headers.XContentTypeOptions = "nosniff";
            return File(file.Content, file.ContentType, file.FileName);
        });

    [HttpDelete("{documentId:guid}")]
    [Authorize(Policy = AuthPolicies.AgencyAdmin)]
    public Task<IActionResult> Delete(Guid reservationId, Guid documentId, CancellationToken ct) =>
        Execute(async (agencyId, actor) =>
        {
            await documents.DeleteAsync(reservationId, documentId, agencyId, actor, ct);
            return NoContent();
        });

    private static async Task<byte[]> ReadAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            throw new LedgerValidationException("file_required", "Upload a file in the 'file' form field.");
        if (file.Length > LedgerDocument.MaxBytes)
            throw new LedgerValidationException("file_too_large",
                $"The file exceeds {LedgerDocument.MaxBytes / 1024 / 1024} MB.");
        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }
}
