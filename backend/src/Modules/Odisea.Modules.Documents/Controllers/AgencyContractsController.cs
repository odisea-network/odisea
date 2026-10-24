using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Documents.Domain;
using Odisea.Modules.Documents.Features.Contracts;
using Odisea.SharedKernel;

namespace Odisea.Modules.Documents.Controllers;

/// Agencies see the contracts shared with them and generate their own from agency-usable templates,
/// always scoped by the agency_id claim (JWT or API key) — never by ids in the request.
[ApiController]
[Route("api/v1/agency/contracts")]
[Authorize(Policy = AuthPolicies.Agency)]
public class AgencyContractsController(ContractService contracts, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct) =>
        Execute(async agencyId => Ok(await contracts.ListForAgencyAsync(agencyId, ct)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Execute(async agencyId => Ok(await contracts.GetForAgencyAsync(id, agencyId, ct)));

    [HttpGet("{id:guid}/file")]
    public Task<IActionResult> GetFile(Guid id, CancellationToken ct) =>
        Execute(async agencyId =>
        {
            var file = await contracts.GetFileForAgencyAsync(id, agencyId, ct);
            return File(file.Content, ContractDtoMapping.DocxContentType, file.FileName);
        });

    [HttpPost]
    public Task<IActionResult> Generate(AgencyGenerateContractRequest request, CancellationToken ct) =>
        Execute(async agencyId =>
        {
            if (currentUser.UserId is not { } userId)
                return Problem(title: "No user context", statusCode: StatusCodes.Status403Forbidden);
            var dto = await contracts.GenerateForAgencyAsync(request, agencyId, userId, ct);
            return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
        });

    private async Task<IActionResult> Execute(Func<Guid, Task<IActionResult>> action)
    {
        if (currentUser.AgencyId is not { } agencyId)
            return Problem(title: "No agency context", statusCode: StatusCodes.Status403Forbidden);
        try
        {
            return await action(agencyId);
        }
        catch (ContractNotFoundException)
        {
            return NotFound();
        }
        catch (ContractValidationException ex)
        {
            return Problem(title: "The contract cannot be generated", detail: ex.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }
}
