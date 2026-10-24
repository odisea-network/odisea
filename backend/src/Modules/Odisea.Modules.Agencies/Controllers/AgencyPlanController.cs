using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.Features.Plans;
using Odisea.Modules.Agencies.Infrastructure;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.SharedKernel;

namespace Odisea.Modules.Agencies.Controllers;

/// What the calling agency is entitled to — the portal uses it to show or upsell workspace features.
[ApiController]
[Route("api/v1/agency/plan")]
[Authorize(Policy = AuthPolicies.Agency)]
public class AgencyPlanController(AgenciesDbContext db, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (currentUser.AgencyId is not { } agencyId)
            return Problem(title: "No agency context", statusCode: StatusCodes.Status403Forbidden);
        var agency = await db.Agencies.AsNoTracking().FirstOrDefaultAsync(a => a.Id == agencyId, ct);
        return agency is null ? NotFound() : Ok(agency.ToPlanDto());
    }
}
