using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Odisea.Modules.Workspace.Domain;
using Odisea.Modules.Workspace.Features.ExternalReservations;

namespace Odisea.Modules.Workspace.Controllers;

/// The customer-facing status page behind an unguessable code: embedded on or linked from the
/// agency's own website, so any origin may call it. Rate limited per IP; never reveals contact data.
[ApiController]
[Route("api/v1/public/track")]
[AllowAnonymous]
[EnableCors(WorkspaceModule.PublicCorsPolicy)]
[EnableRateLimiting(WorkspaceModule.PublicRateLimitPolicy)]
public class PublicTrackingController(ExternalReservationService reservations) : ControllerBase
{
    [HttpGet("{code}")]
    public async Task<ActionResult<PublicTrackingDto>> Track(string code, CancellationToken ct)
    {
        try
        {
            var dto = await reservations.TrackAsync(code, ct);
            Response.Headers.CacheControl = "no-store";
            return Ok(dto);
        }
        catch (WorkspaceNotFoundException)
        {
            return NotFound();
        }
    }
}
