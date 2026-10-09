using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Documents.Domain;
using Odisea.Modules.Documents.Features.Issuing;
using Odisea.Modules.Documents.Features.Pdf;
using Odisea.Modules.Documents.Infrastructure;

namespace Odisea.Modules.Documents.Controllers;

[ApiController]
[Route("api/v1/documents")]
[Authorize(Policy = AuthPolicies.Operator)]
public class DocumentsController(
    DocumentsDbContext db,
    DocumentService documents,
    DocumentPdfRenderer pdf) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<FinancialDocumentDto>>> List(
        [FromQuery] Guid? agencyId, [FromQuery] DocumentKind? kind, CancellationToken ct)
    {
        var query = db.FinancialDocuments.AsNoTracking();
        if (agencyId is { } a)
            query = query.Where(d => d.AgencyId == a);
        if (kind is { } k)
            query = query.Where(d => d.Kind == k);

        var items = await query.OrderByDescending(d => d.Number).Take(200).ToListAsync(ct);
        return Ok(items.Select(d => d.ToDto()).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FinancialDocumentDto>> Get(Guid id, CancellationToken ct)
    {
        var document = await db.FinancialDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        return document is null ? NotFound() : Ok(document.ToDto());
    }

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> GetPdf(Guid id, CancellationToken ct)
    {
        var document = await db.FinancialDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (document is null)
            return NotFound();
        return File(pdf.Render(document), "application/pdf", $"{document.Kind}-{document.Number}.pdf");
    }

    [HttpPost("invoices")]
    public Task<IActionResult> IssueInvoice(IssueInvoiceRequest request, CancellationToken ct) =>
        Execute(async () =>
        {
            var dto = await documents.IssueInvoiceAsync(request, ct);
            return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
        });

    [HttpPost("credit-notes")]
    public Task<IActionResult> IssueCreditNote(IssueCreditNoteRequest request, CancellationToken ct) =>
        Execute(async () =>
        {
            var dto = await documents.IssueCreditNoteAsync(request, ct);
            return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
        });

    [HttpPost("{id:guid}/annul")]
    public Task<IActionResult> Annul(Guid id, AnnulRequest request, CancellationToken ct) =>
        Execute(async () => Ok(await documents.AnnulAsync(id, request.Reason, ct)));

    private async Task<IActionResult> Execute(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (DocumentValidationException ex)
        {
            return Problem(title: "Документът не може да бъде издаден", detail: ex.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }
}
