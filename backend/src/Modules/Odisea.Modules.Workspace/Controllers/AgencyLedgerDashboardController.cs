using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Workspace.Features.Ledger;
using Odisea.SharedKernel;

namespace Odisea.Modules.Workspace.Controllers;

/// The caller's agency at a glance: what is owed, what is due soon, what is late.
[ApiController]
[Route("api/v1/agency/ledger/dashboard")]
[Authorize(Policy = AuthPolicies.Agency)]
public class AgencyLedgerDashboardController(LedgerDashboardService dashboard, ICurrentUser currentUser)
    : LedgerControllerBase(currentUser)
{
    [HttpGet]
    public Task<IActionResult> Get(CancellationToken ct) =>
        Execute(async (agencyId, _) => Ok(await dashboard.GetAsync(agencyId, ct)));
}
