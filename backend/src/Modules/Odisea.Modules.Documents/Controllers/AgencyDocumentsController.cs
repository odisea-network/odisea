using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Documents.Features.Issuing;
using Odisea.Modules.Documents.Features.Pdf;
using Odisea.Modules.Documents.Infrastructure;
using Odisea.SharedKernel;

namespace Odisea.Modules.Documents.Controllers;

/// Agencies see and download ONLY their own documents (agency_id from the JWT).
[ApiController]
[Route("api/v1/agency/documents")]
[Authorize(Policy = AuthPolicies.Agency)]
public class AgencyDocumentsController(
    DocumentsDbContext db,
    DocumentPdfRenderer pdf,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (currentUser.AgencyId is not { } agencyId)
            return Problem(title: "No agency context", statusCode: StatusCodes.Status403Forbidden);

        var items = await db.FinancialDocuments.AsNoTracking()
            .Where(d => d.AgencyId == agencyId)
            .OrderByDescending(d => d.Number)
            .ToListAsync(ct);
        return Ok(items.Select(d => d.ToDto()).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        if (currentUser.AgencyId is not { } agencyId)
            return Problem(title: "No agency context", statusCode: StatusCodes.Status403Forbidden);

        var document = await db.FinancialDocuments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.AgencyId == agencyId, ct);
        return document is null ? NotFound() : Ok(document.ToDto());
    }

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> GetPdf(Guid id, CancellationToken ct)
    {
        if (currentUser.AgencyId is not { } agencyId)
            return Problem(title: "No agency context", statusCode: StatusCodes.Status403Forbidden);

        var document = await db.FinancialDocuments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.AgencyId == agencyId, ct);
        if (document is null)
            return NotFound();
        return File(pdf.Render(document), "application/pdf", $"{document.Kind}-{document.Number}.pdf");
    }
}
