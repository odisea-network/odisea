using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Documents.Domain;
using Odisea.Modules.Documents.Features.Contracts;
using Odisea.SharedKernel;

namespace Odisea.Modules.Documents.Controllers;

/// Word templates for contracts. Reading is for every operator; changing the legal text is an admin matter.
[ApiController]
[Route("api/v1/contract-templates")]
[Authorize(Policy = AuthPolicies.Operator)]
public class ContractTemplatesController(ContractService contracts, ICurrentUser currentUser) : ControllerBase
{
    private const long UploadLimit = DocxTemplateRenderer.MaxTemplateBytes + 64 * 1024;

    [HttpGet]
    public async Task<ActionResult<List<ContractTemplateDto>>> List([FromQuery] bool includeInactive, CancellationToken ct) =>
        Ok(await contracts.ListTemplatesAsync(includeInactive, ct));

    /// The placeholders the API fills by itself; anything else in a template is a custom field.
    [HttpGet("placeholders")]
    public ActionResult<List<PlaceholderDto>> Placeholders() =>
        Ok(PlaceholderCatalogue.All.Select(p => p.ToDto()).ToList());

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Execute(async () => Ok(await contracts.GetTemplateAsync(id, ct)));

    [HttpGet("{id:guid}/file")]
    public Task<IActionResult> GetFile(Guid id, CancellationToken ct) =>
        Execute(async () =>
        {
            var file = await contracts.GetTemplateFileAsync(id, ct);
            return File(file.Content, ContractDtoMapping.DocxContentType, file.FileName);
        });

    [HttpPost]
    [Authorize(Policy = AuthPolicies.OperatorAdmin)]
    [RequestSizeLimit(UploadLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimit)]
    public Task<IActionResult> Upload(
        IFormFile? file,
        [FromForm] string? name,
        [FromForm] string? kind,
        [FromForm] string? description,
        [FromForm] bool agencyUsable,
        CancellationToken ct) =>
        Execute(async () =>
        {
            if (currentUser.UserId is not { } userId)
                return Problem(title: "No user context", statusCode: StatusCodes.Status403Forbidden);
            var bytes = await ReadAsync(file, ct);
            var dto = await contracts.UploadTemplateAsync(name, kind, description, agencyUsable, bytes, file?.FileName, userId, ct);
            return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
        });

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthPolicies.OperatorAdmin)]
    public Task<IActionResult> Update(Guid id, UpdateContractTemplateRequest request, CancellationToken ct) =>
        Execute(async () => Ok(await contracts.UpdateTemplateAsync(id, request, ct)));

    [HttpPut("{id:guid}/file")]
    [Authorize(Policy = AuthPolicies.OperatorAdmin)]
    [RequestSizeLimit(UploadLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimit)]
    public Task<IActionResult> ReplaceFile(Guid id, IFormFile? file, CancellationToken ct) =>
        Execute(async () =>
        {
            var bytes = await ReadAsync(file, ct);
            return Ok(await contracts.ReplaceTemplateFileAsync(id, bytes, file?.FileName, ct));
        });

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthPolicies.OperatorAdmin)]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        Execute(async () =>
        {
            await contracts.DeleteTemplateAsync(id, ct);
            return NoContent();
        });

    private static async Task<byte[]> ReadAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            throw new ContractValidationException("Upload a .docx file in the 'file' form field.");
        if (file.Length > DocxTemplateRenderer.MaxTemplateBytes)
            throw new ContractValidationException($"The template exceeds {DocxTemplateRenderer.MaxTemplateBytes / 1024 / 1024} MB.");
        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }

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
        catch (ContractTemplateInUseException ex)
        {
            return Problem(title: "Template in use", detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (ContractValidationException ex)
        {
            return Problem(title: "Invalid template", detail: ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }
}
