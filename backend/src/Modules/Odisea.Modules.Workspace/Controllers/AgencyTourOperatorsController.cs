using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Workspace.Features.Ledger;
using Odisea.SharedKernel;

namespace Odisea.Modules.Workspace.Controllers;

/// The agency's own tour operators; always the caller's agency.
[ApiController]
[Route("api/v1/agency/tour-operators")]
[Authorize(Policy = AuthPolicies.Agency)]
public class AgencyTourOperatorsController(TourOperatorService operators, ICurrentUser currentUser)
    : LedgerControllerBase(currentUser)
{
    [HttpGet]
    public Task<IActionResult> List([FromQuery] bool includeArchived, CancellationToken ct) =>
        Execute(async (agencyId, _) => Ok(await operators.ListAsync(agencyId, includeArchived, ct)));

    [HttpPost]
    public Task<IActionResult> Create(SaveTourOperatorRequest request, CancellationToken ct) =>
        Execute(async (agencyId, _) =>
            StatusCode(StatusCodes.Status201Created, await operators.CreateAsync(agencyId, request, ct)));

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, SaveTourOperatorRequest request, CancellationToken ct) =>
        Execute(async (agencyId, _) => Ok(await operators.UpdateAsync(id, agencyId, request, ct)));

    [HttpPost("{id:guid}/archive")]
    [Authorize(Policy = AuthPolicies.AgencyAdmin)]
    public Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        Execute(async (agencyId, actor) => Ok(await operators.SetArchivedAsync(id, agencyId, actor, archived: true, ct)));

    [HttpPost("{id:guid}/unarchive")]
    [Authorize(Policy = AuthPolicies.AgencyAdmin)]
    public Task<IActionResult> Unarchive(Guid id, CancellationToken ct) =>
        Execute(async (agencyId, actor) => Ok(await operators.SetArchivedAsync(id, agencyId, actor, archived: false, ct)));
}
