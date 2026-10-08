using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Catalog.Domain;
using Odisea.Modules.Catalog.Features.Programs;
using Odisea.Modules.Catalog.Infrastructure;

namespace Odisea.Modules.Catalog.Controllers;

/// The agency-facing view of the catalog: published programs only.
[ApiController]
[Route("api/v1/catalog/programs")]
[Authorize(Policy = AuthPolicies.Agency)]
public class ProgramCatalogController(CatalogDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProgramDto>>> List(CancellationToken ct) =>
        Ok(await db.Programs
            .Where(p => p.Status == ProgramStatus.Published)
            .OrderBy(p => p.Name)
            .Select(p => p.ToDto())
            .ToListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProgramDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var program = await db.Programs
            .Include(p => p.Departures)
            .Include(p => p.Hotels)
            .FirstOrDefaultAsync(p => p.Id == id && p.Status == ProgramStatus.Published, ct);
        return program is null ? NotFound() : Ok(program.ToDetailDto());
    }
}
