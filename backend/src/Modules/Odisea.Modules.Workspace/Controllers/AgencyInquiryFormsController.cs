using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Workspace.Domain;
using Odisea.Modules.Workspace.Features.Inquiries;
using Odisea.SharedKernel;

namespace Odisea.Modules.Workspace.Controllers;

/// The agency's website forms: configuration, keys and the inquiries they bring in.
[ApiController]
[Authorize(Policy = AuthPolicies.Agency)]
public class AgencyInquiryFormsController(InquiryFormService forms, ICurrentUser currentUser) : ControllerBase
{
    // ----- forms -----

    [HttpGet("api/v1/agency/forms")]
    public Task<IActionResult> ListForms(CancellationToken ct) =>
        Execute(async (agencyId, _) => Ok(await forms.ListFormsAsync(agencyId, ct)));

    [HttpGet("api/v1/agency/forms/{id:guid}")]
    public Task<IActionResult> GetForm(Guid id, CancellationToken ct) =>
        Execute(async (agencyId, _) => Ok(await forms.GetFormAsync(id, agencyId, ct)));

    [HttpPost("api/v1/agency/forms")]
    public Task<IActionResult> CreateForm(SaveInquiryFormRequest request, CancellationToken ct) =>
        Execute(async (agencyId, _) =>
        {
            var dto = await forms.CreateFormAsync(agencyId, request, ct);
            return CreatedAtAction(nameof(GetForm), new { id = dto.Id }, dto);
        });

    [HttpPut("api/v1/agency/forms/{id:guid}")]
    public Task<IActionResult> UpdateForm(Guid id, SaveInquiryFormRequest request, CancellationToken ct) =>
        Execute(async (agencyId, _) => Ok(await forms.UpdateFormAsync(id, agencyId, request, ct)));

    [HttpPost("api/v1/agency/forms/{id:guid}/rotate-key")]
    public Task<IActionResult> RotateKey(Guid id, CancellationToken ct) =>
        Execute(async (agencyId, _) => Ok(await forms.RotateKeyAsync(id, agencyId, ct)));

    [HttpDelete("api/v1/agency/forms/{id:guid}")]
    public Task<IActionResult> DeleteForm(Guid id, CancellationToken ct) =>
        Execute(async (agencyId, _) =>
        {
            await forms.DeleteFormAsync(id, agencyId, ct);
            return NoContent();
        });

    // ----- inquiries -----

    [HttpGet("api/v1/agency/inquiries")]
    public Task<IActionResult> ListInquiries(
        [FromQuery] Guid? formId, [FromQuery] InquiryStatus? status, [FromQuery] string? q,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Execute(async (agencyId, _) => Ok(await forms.ListAsync(agencyId, formId, status, q, from, to, page, pageSize, ct)));

    [HttpGet("api/v1/agency/inquiries/{id:guid}")]
    public Task<IActionResult> GetInquiry(Guid id, CancellationToken ct) =>
        Execute(async (agencyId, _) => Ok(await forms.GetAsync(id, agencyId, ct)));

    [HttpPost("api/v1/agency/inquiries/{id:guid}/status")]
    public Task<IActionResult> SetStatus(Guid id, SetInquiryStatusRequest request, CancellationToken ct) =>
        Execute(async (agencyId, userId) => Ok(await forms.SetStatusAsync(id, agencyId, userId, request, ct)));

    private async Task<IActionResult> Execute(Func<Guid, Guid, Task<IActionResult>> action)
    {
        if (currentUser.AgencyId is not { } agencyId || currentUser.UserId is not { } userId)
            return Problem(title: "No agency context", statusCode: StatusCodes.Status403Forbidden);
        try
        {
            return await action(agencyId, userId);
        }
        catch (WorkspaceNotFoundException)
        {
            return NotFound();
        }
        catch (FormInUseException ex)
        {
            return Problem(title: "Form in use", detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (WorkspaceValidationException ex)
        {
            return Problem(title: "Invalid form", detail: ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
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
