using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Workspace.Domain;
using Odisea.Modules.Workspace.Features.ExternalReservations;
using Odisea.SharedKernel;

namespace Odisea.Modules.Workspace.Controllers;

/// The agency's own external reservations. Portal users and API keys alike; always the caller's agency.
[ApiController]
[Route("api/v1/agency/external-reservations")]
[Authorize(Policy = AuthPolicies.Agency)]
public class AgencyExternalReservationsController(ExternalReservationService reservations, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] string? q, [FromQuery] DateOnly? travelFrom, [FromQuery] DateOnly? travelTo,
        [FromQuery] string? tag, [FromQuery] bool includeArchived, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Execute(async (agencyId, _) => Ok(await reservations.ListAsync(
            agencyId, status, q, travelFrom, travelTo, tag, includeArchived, page, pageSize, ct)));

    [HttpGet("statuses")]
    public Task<IActionResult> Statuses(CancellationToken ct) =>
        Execute(async (agencyId, _) => Ok(await reservations.StatusesAsync(agencyId, ct)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Execute(async (agencyId, _) => Ok(await reservations.GetAsync(id, agencyId, ct)));

    [HttpPost]
    public Task<IActionResult> Create(CreateExternalReservationRequest request, CancellationToken ct) =>
        Execute(async (agencyId, actor) =>
        {
            var dto = await reservations.CreateAsync(agencyId, actor, request, ct);
            return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
        });

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, UpdateExternalReservationRequest request, CancellationToken ct) =>
        Execute(async (agencyId, actor) => Ok(await reservations.UpdateAsync(id, agencyId, actor, request, ct)));

    [HttpPost("{id:guid}/status")]
    public Task<IActionResult> ChangeStatus(Guid id, ChangeStatusRequest request, CancellationToken ct) =>
        Execute(async (agencyId, actor) => Ok(await reservations.ChangeStatusAsync(id, agencyId, actor, request, ct)));

    [HttpPost("{id:guid}/notes")]
    public Task<IActionResult> AddNote(Guid id, AddNoteRequest request, CancellationToken ct) =>
        Execute(async (agencyId, actor) => Ok(await reservations.AddNoteAsync(id, agencyId, actor, request, ct)));

    [HttpPost("{id:guid}/archive")]
    public Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        Execute(async (agencyId, actor) => Ok(await reservations.SetArchivedAsync(id, agencyId, actor, archived: true, ct)));

    [HttpPost("{id:guid}/unarchive")]
    public Task<IActionResult> Unarchive(Guid id, CancellationToken ct) =>
        Execute(async (agencyId, actor) => Ok(await reservations.SetArchivedAsync(id, agencyId, actor, archived: false, ct)));

    private async Task<IActionResult> Execute(Func<Guid, WorkspaceActor, Task<IActionResult>> action)
    {
        if (currentUser.AgencyId is not { } agencyId || currentUser.UserId is not { } userId)
            return Problem(title: "No agency context", statusCode: StatusCodes.Status403Forbidden);
        var actor = new WorkspaceActor(currentUser.IsApiClient ? WorkspaceActor.ApiClient : WorkspaceActor.Agency, userId);
        try
        {
            return await action(agencyId, actor);
        }
        catch (WorkspaceNotFoundException)
        {
            return NotFound();
        }
        catch (DuplicateReferenceException ex)
        {
            return Problem(title: "Duplicate reference", detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (WorkspaceValidationException ex)
        {
            return Problem(title: "Invalid reservation", detail: ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (FeatureNotInPlanException ex)
        {
            return Problem(type: ProblemTypes.FeatureNotInPlan, title: "Not included in the agency's plan",
                detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (AgencySuspendedException ex)
        {
            return Problem(type: ProblemTypes.AgencySuspended, title: "Agency suspended",
                detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    }
}
