using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Catalog.Domain;
using Odisea.Modules.Catalog.Features.Destinations;
using Odisea.Modules.Catalog.Infrastructure;

namespace Odisea.Modules.Catalog.Controllers;

[ApiController]
[Route("api/v1/destinations")]
[Authorize(Policy = AuthPolicies.Operator)]
public class DestinationsController(CatalogDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<DestinationDto>>> List(CancellationToken ct) =>
        Ok(await db.Destinations.OrderBy(d => d.Name).Select(d => d.ToDto()).ToListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<DestinationDto>> Create(CreateDestinationRequest request, CancellationToken ct)
    {
        var destination = new Destination { Name = request.Name, Country = request.Country.ToUpperInvariant() };
        db.Destinations.Add(destination);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), null, destination.ToDto());
    }

    [HttpGet("{id:guid}/mappings")]
    public async Task<ActionResult<List<LocationMappingDto>>> Mappings(Guid id, CancellationToken ct) =>
        Ok(await db.LocationMappings.Where(m => m.DestinationId == id).Select(m => m.ToDto()).ToListAsync(ct));

    [HttpPost("{id:guid}/mappings")]
    public async Task<ActionResult<LocationMappingDto>> AddMapping(
        Guid id, CreateLocationMappingRequest request, CancellationToken ct)
    {
        if (!await db.Destinations.AnyAsync(d => d.Id == id, ct))
            return NotFound();

        var provider = request.ProviderCode.ToLowerInvariant();
        if (await db.LocationMappings.AnyAsync(m => m.DestinationId == id && m.ProviderCode == provider, ct))
            return Problem(title: "Duplicate mapping",
                detail: $"Destination already mapped for provider '{provider}'.",
                statusCode: StatusCodes.Status409Conflict);

        var mapping = new LocationMapping
        {
            DestinationId = id,
            ProviderCode = provider,
            ExternalLocationCode = request.ExternalLocationCode,
        };
        db.LocationMappings.Add(mapping);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Mappings), new { id }, mapping.ToDto());
    }
}
