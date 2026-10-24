using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Workspace.Domain;
using Odisea.Modules.Workspace.Features.ExternalReservations;
using Odisea.Modules.Workspace.Features.Usage;

namespace Odisea.Modules.Workspace.Controllers;

/// Operator oversight of the workspace tier: usage per agency (billing input) and a read-only list for support.
[ApiController]
[Route("api/v1/admin/workspace")]
[Authorize(Policy = AuthPolicies.Operator)]
public class AdminWorkspaceController(ExternalReservationService reservations, WorkspaceUsageService usage) : ControllerBase
{
    /// Counts per agency for a month (yyyy-MM, default: the current month).
    [HttpGet("usage")]
    public async Task<IActionResult> Usage([FromQuery] string? month, CancellationToken ct)
    {
        try
        {
            return Ok(await usage.UsageAsync(month, ct));
        }
        catch (WorkspaceValidationException ex)
        {
            return Problem(title: "Invalid month", detail: ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    [HttpGet("external-reservations")]
    public async Task<ActionResult<PagedResult<ExternalReservationSummaryDto>>> ExternalReservations(
        [FromQuery] Guid? agencyId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        Ok(await reservations.ListForOperatorAsync(agencyId, page, pageSize, ct));
}
