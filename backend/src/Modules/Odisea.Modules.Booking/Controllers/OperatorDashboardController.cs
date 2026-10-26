using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Booking.Features.Bookings;

namespace Odisea.Modules.Booking.Controllers;

/// The operator's "Today" figures. Read-only; every amount is summed here, never in the portal.
[ApiController]
[Route("api/v1/admin/dashboard")]
[Authorize(Policy = AuthPolicies.Operator)]
public class OperatorDashboardController(OperatorDashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<OperatorDashboardDto>> Get([FromQuery] OperatorDashboardQuery query, CancellationToken ct) =>
        Ok(await dashboard.BuildAsync(query.Days, ct));
}
