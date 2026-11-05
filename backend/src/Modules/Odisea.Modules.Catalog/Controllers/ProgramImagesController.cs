using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Catalog.Domain;
using Odisea.Modules.Catalog.Features.Programs;

namespace Odisea.Modules.Catalog.Controllers;

/// The gallery of one program. Metadata only — the bytes are served by PublicProgramImagesController.
[ApiController]
[Route("api/v1/programs/{programId:guid}/images")]
[Authorize(Policy = AuthPolicies.Operator)]
public class ProgramImagesController(ProgramImageService images) : ControllerBase
{
    private const long UploadLimit = ProgramImage.MaxBytes + 64 * 1024;

    [HttpGet]
    public async Task<ActionResult<List<ProgramImageDto>>> List(Guid programId, CancellationToken ct) =>
        Ok(await images.ListAsync(programId, ct));

    [HttpPost]
    [RequestSizeLimit(UploadLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimit)]
    public Task<IActionResult> Upload(
        Guid programId,
        IFormFile? file,
        [FromForm] string? caption,
        [FromForm] string? altText,
        [FromForm] bool isCover,
        CancellationToken ct) =>
        Execute(async () =>
        {
            var bytes = await ReadAsync(file, ct);
            var dto = await images.UploadAsync(programId, bytes, caption, altText, isCover, ct);
            return CreatedAtAction(nameof(List), new { programId }, dto);
        });

    [HttpPut("order")]
    public Task<IActionResult> Reorder(Guid programId, ReorderProgramImagesRequest request, CancellationToken ct) =>
        Execute(async () => Ok(await images.ReorderAsync(programId, request.ImageIds, ct)));

    [HttpPut("{imageId:guid}")]
    public Task<IActionResult> Update(Guid programId, Guid imageId, UpdateProgramImageRequest request, CancellationToken ct) =>
        Execute(async () => Ok(await images.UpdateAsync(programId, imageId, request, ct)));

    [HttpDelete("{imageId:guid}")]
    public Task<IActionResult> Delete(Guid programId, Guid imageId, CancellationToken ct) =>
        Execute(async () =>
        {
            await images.DeleteAsync(programId, imageId, ct);
            return NoContent();
        });

    private static async Task<byte[]> ReadAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            throw new ProgramValidationException("Upload an image file in the 'file' form field.");
        if (file.Length > ProgramImage.MaxBytes)
            throw new ProgramValidationException($"The image exceeds {ProgramImage.MaxBytes / 1024 / 1024} MB.");
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
        catch (ProgramImageNotFoundException ex)
        {
            return Problem(title: "Not found", detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (ProgramValidationException ex)
        {
            return Problem(title: "Invalid image", detail: ex.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }
}
