using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.Features.Branding;
using Odisea.Modules.Agencies.PublicApi;

namespace Odisea.Modules.Agencies.Controllers;

/// The white-label portal dresses itself from here. Reads are anonymous and cacheable (the login
/// page needs them before any token); changes are an operator-admin decision.
[ApiController]
[Route("api/v1/branding")]
public class BrandingController(BrandingService branding) : ControllerBase
{
    private const int LogoRequestLimit = BrandingService.MaxLogoBytes + 64 * 1024;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<BrandingDto>> Get(CancellationToken ct)
    {
        // Overrides the API-wide no-store: branding is public and changes rarely.
        Response.Headers.CacheControl = "public, max-age=300";
        return Ok(await branding.GetAsync(ct));
    }

    [HttpGet("logo")]
    [AllowAnonymous]
    public async Task<IActionResult> GetLogo(CancellationToken ct)
    {
        var logo = await branding.GetLogoAsync(ct);
        if (logo is null)
            return NotFound();

        var etag = $"\"{logo.Hash}\"";
        Response.Headers.CacheControl = "public, max-age=86400";
        Response.Headers.ETag = etag;
        if (Request.Headers.IfNoneMatch.Any(tag => tag is not null && tag.Split(',').Select(t => t.Trim()).Contains(etag)))
            return StatusCode(StatusCodes.Status304NotModified);

        return File(logo.Bytes, logo.ContentType);
    }

    [HttpPut]
    [Authorize(Policy = AuthPolicies.OperatorAdmin)]
    public async Task<ActionResult<BrandingDto>> Update(UpdateBrandingRequest request, CancellationToken ct) =>
        Ok(await branding.UpdateAsync(request, ct));

    [HttpPut("logo")]
    [Authorize(Policy = AuthPolicies.OperatorAdmin)]
    [RequestSizeLimit(LogoRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = LogoRequestLimit)]
    public async Task<ActionResult<BrandingDto>> UploadLogo(IFormFile? file, CancellationToken ct)
    {
        if (file is null)
            return Problem(title: "No file", detail: "Send the logo as multipart form field 'file'.",
                statusCode: StatusCodes.Status400BadRequest);

        try
        {
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, ct);
            return Ok(await branding.SetLogoAsync(buffer.ToArray(), file.ContentType, ct));
        }
        catch (BrandingValidationException ex)
        {
            return Problem(title: "Logo rejected", detail: ex.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    [HttpDelete("logo")]
    [Authorize(Policy = AuthPolicies.OperatorAdmin)]
    public async Task<ActionResult<BrandingDto>> RemoveLogo(CancellationToken ct) =>
        Ok(await branding.RemoveLogoAsync(ct));
}
