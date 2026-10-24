using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Documents.Domain;
using Odisea.Modules.Documents.Features.Contracts;
using Odisea.SharedKernel;

namespace Odisea.Modules.Documents.Controllers;

/// Generated contracts: filled Word documents kept as the record of what was handed out.
[ApiController]
[Route("api/v1/contracts")]
[Authorize(Policy = AuthPolicies.Operator)]
public class ContractsController(ContractService contracts, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<GeneratedContractDto>>> List(
        [FromQuery] Guid? bookingId, [FromQuery] Guid? agencyId, [FromQuery] Guid? templateId, CancellationToken ct) =>
        Ok(await contracts.ListGeneratedAsync(bookingId, agencyId, templateId, ct));

    /// The values a template would receive for this booking and/or agency.
    [HttpGet("data")]
    public Task<IActionResult> PreviewData([FromQuery] Guid? bookingId, [FromQuery] Guid? agencyId, CancellationToken ct) =>
        Execute(async () => Ok(await contracts.PreviewDataAsync(bookingId, agencyId, ct)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Execute(async () => Ok(await contracts.GetGeneratedAsync(id, ct)));

    [HttpGet("{id:guid}/file")]
    public Task<IActionResult> GetFile(Guid id, CancellationToken ct) =>
        Execute(async () =>
        {
            var file = await contracts.GetGeneratedFileAsync(id, ct);
            return File(file.Content, ContractDtoMapping.DocxContentType, file.FileName);
        });

    [HttpPost]
    public Task<IActionResult> Generate(GenerateContractRequest request, CancellationToken ct) =>
        Execute(async () =>
        {
            if (currentUser.UserId is not { } userId)
                return Problem(title: "No user context", statusCode: StatusCodes.Status403Forbidden);
            var dto = await contracts.GenerateAsync(request, userId, ct);
            return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
        });

    [HttpPost("{id:guid}/share")]
    public Task<IActionResult> Share(Guid id, CancellationToken ct) =>
        Execute(async () => Ok(await contracts.SetSharedAsync(id, shared: true, ct)));

    [HttpPost("{id:guid}/unshare")]
    public Task<IActionResult> Unshare(Guid id, CancellationToken ct) =>
        Execute(async () => Ok(await contracts.SetSharedAsync(id, shared: false, ct)));

    private async Task<IActionResult> Execute(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ContractNotFoundException ex)
        {
            return Problem(title: "Not found", detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (ContractValidationException ex)
        {
            return Problem(title: "The contract cannot be generated", detail: ex.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }
}
