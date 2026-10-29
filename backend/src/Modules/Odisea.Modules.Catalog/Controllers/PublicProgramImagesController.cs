using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Odisea.Modules.Catalog.Features.Programs;

namespace Odisea.Modules.Catalog.Controllers;

/// Program photos are marketing material: served without a login so portal `<img>` tags and agency
/// websites can embed them. Ids are unguessable, the bytes behind an id never change, and nothing
/// but the picture is returned — no program data, no status.
[ApiController]
[Route("api/v1/public/program-images")]
[AllowAnonymous]
public class PublicProgramImagesController(ProgramImageService images) : ControllerBase
{
    [HttpGet("{imageId:guid}")]
    public async Task<IActionResult> Get(Guid imageId, CancellationToken ct)
    {
        var image = await images.GetFileAsync(imageId, ct);
        if (image is null)
            return NotFound();

        var etag = $"\"{image.Sha256}\"";
        // Overrides the API-wide no-store: a deleted image 404s, a changed one gets a new id.
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        Response.Headers.ETag = etag;
        if (Request.Headers.IfNoneMatch.Any(tag => tag is not null && tag.Split(',').Select(t => t.Trim()).Contains(etag)))
            return StatusCode(StatusCodes.Status304NotModified);

        return File(image.Data, image.ContentType);
    }
}
