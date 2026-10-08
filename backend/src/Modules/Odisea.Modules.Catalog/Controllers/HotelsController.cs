using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Catalog.Domain;
using Odisea.Modules.Catalog.Features.Hotels;
using Odisea.Modules.Catalog.Infrastructure;

namespace Odisea.Modules.Catalog.Controllers;

[ApiController]
[Route("api/v1/hotels")]
[Authorize(Policy = AuthPolicies.Operator)]
public class HotelsController(CatalogDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<HotelDto>>> List([FromQuery] Guid? destinationId, CancellationToken ct)
    {
        var query = db.Hotels.AsQueryable();
        if (destinationId is { } dest)
            query = query.Where(h => h.DestinationId == dest);
        return Ok(await query.OrderBy(h => h.Name).Select(h => h.ToDto()).ToListAsync(ct));
    }

    [HttpPost]
    public async Task<ActionResult<HotelDto>> Create(CreateHotelRequest request, CancellationToken ct)
    {
        if (!await db.Destinations.AnyAsync(d => d.Id == request.DestinationId, ct))
            return Problem(title: "Unknown destination",
                detail: $"Destination {request.DestinationId} does not exist.",
                statusCode: StatusCodes.Status422UnprocessableEntity);

        var hotel = new Hotel { Name = request.Name, DestinationId = request.DestinationId, Stars = request.Stars };
        db.Hotels.Add(hotel);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), null, hotel.ToDto());
    }

    [HttpGet("{id:guid}/mappings")]
    public async Task<ActionResult<List<HotelMappingDto>>> Mappings(Guid id, CancellationToken ct) =>
        Ok(await db.HotelMappings.Where(m => m.HotelId == id).Select(m => m.ToDto()).ToListAsync(ct));

    [HttpPost("{id:guid}/mappings")]
    public async Task<ActionResult<HotelMappingDto>> AddMapping(
        Guid id, CreateHotelMappingRequest request, CancellationToken ct)
    {
        if (!await db.Hotels.AnyAsync(h => h.Id == id, ct))
            return NotFound();

        var provider = request.ProviderCode.ToLowerInvariant();
        var duplicate = await db.HotelMappings.AnyAsync(m =>
            (m.HotelId == id && m.ProviderCode == provider) ||
            (m.ProviderCode == provider && m.ExternalHotelCode == request.ExternalHotelCode), ct);
        if (duplicate)
            return Problem(title: "Duplicate mapping",
                detail: $"Hotel or external code already mapped for provider '{provider}'.",
                statusCode: StatusCodes.Status409Conflict);

        var mapping = new HotelMapping
        {
            HotelId = id,
            ProviderCode = provider,
            ExternalHotelCode = request.ExternalHotelCode,
        };
        db.HotelMappings.Add(mapping);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Mappings), new { id }, mapping.ToDto());
    }
}
