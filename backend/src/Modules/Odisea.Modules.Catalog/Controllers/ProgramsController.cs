using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Catalog.Domain;
using Odisea.Modules.Catalog.Features.Programs;
using Odisea.Modules.Catalog.Infrastructure;
using Odisea.Modules.Integrations.PublicApi;

namespace Odisea.Modules.Catalog.Controllers;

[ApiController]
[Route("api/v1/programs")]
[Authorize(Policy = AuthPolicies.Operator)]
public class ProgramsController(CatalogDbContext db, IProviderRegistry providers) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProgramDto>>> List([FromQuery] ProgramStatus? status, CancellationToken ct)
    {
        var query = db.Programs.AsQueryable();
        if (status is { } s)
            query = query.Where(p => p.Status == s);
        return Ok(await query.OrderBy(p => p.Name).Select(p => p.ToDto()).ToListAsync(ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProgramDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var program = await db.Programs
            .Include(p => p.Departures)
            .Include(p => p.Hotels)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        return program is null ? NotFound() : Ok(program.ToDetailDto());
    }

    [HttpPost]
    public async Task<ActionResult<ProgramDetailDto>> Create(CreateProgramRequest request, CancellationToken ct)
    {
        if (!await db.Destinations.AnyAsync(d => d.Id == request.DestinationId, ct))
            return Problem(title: "Unknown destination",
                detail: $"Destination {request.DestinationId} does not exist.",
                statusCode: StatusCodes.Status422UnprocessableEntity);

        var providerCode = request.ProviderCode.ToLowerInvariant();
        if (!providers.KnownProviderCodes.Contains(providerCode, StringComparer.OrdinalIgnoreCase))
            return Problem(title: "Unknown provider",
                detail: $"No reservation provider '{providerCode}' is registered.",
                statusCode: StatusCodes.Status422UnprocessableEntity);

        var program = new Domain.Program
        {
            Name = request.Name,
            DestinationId = request.DestinationId,
            ProviderCode = providerCode,
            Season = request.Season,
            Description = request.Description,
        };
        db.Programs.Add(program);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = program.Id }, program.ToDetailDto());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProgramDetailDto>> Update(Guid id, UpdateProgramRequest request, CancellationToken ct)
    {
        var program = await db.Programs
            .Include(p => p.Departures)
            .Include(p => p.Hotels)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (program is null)
            return NotFound();

        program.Name = request.Name;
        program.Season = request.Season;
        program.Description = request.Description;
        await db.SaveChangesAsync(ct);
        return Ok(program.ToDetailDto());
    }

    [HttpPost("{id:guid}/departures")]
    public async Task<ActionResult<DepartureDto>> AddDeparture(Guid id, CreateDepartureRequest request, CancellationToken ct)
    {
        if (request.EndDate <= request.StartDate)
            return Problem(title: "Invalid period", detail: "EndDate must be after StartDate.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        if (!await db.Programs.AnyAsync(p => p.Id == id, ct))
            return NotFound();

        var departure = new ProgramDeparture
        {
            ProgramId = id,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            TransportNote = request.TransportNote,
        };
        db.ProgramDepartures.Add(departure);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id }, departure.ToDto());
    }

    [HttpPost("{id:guid}/hotels/{hotelId:guid}")]
    public async Task<IActionResult> AttachHotel(Guid id, Guid hotelId, CancellationToken ct)
    {
        var program = await db.Programs.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (program is null)
            return NotFound();
        if (!await db.Hotels.AnyAsync(h => h.Id == hotelId, ct))
            return Problem(title: "Unknown hotel", detail: $"Hotel {hotelId} does not exist.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        if (await db.ProgramHotels.AnyAsync(ph => ph.ProgramId == id && ph.HotelId == hotelId, ct))
            return Problem(title: "Already attached", detail: "This hotel is already allowed in the program.",
                statusCode: StatusCodes.Status409Conflict);

        db.ProgramHotels.Add(new ProgramHotel { ProgramId = id, HotelId = hotelId });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<ActionResult<ProgramDetailDto>> Publish(Guid id, CancellationToken ct)
    {
        var program = await db.Programs
            .Include(p => p.Departures)
            .Include(p => p.Hotels)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (program is null)
            return NotFound();

        try
        {
            program.Publish();
        }
        catch (InvalidProgramStateException ex)
        {
            return Problem(title: "Cannot publish", detail: ex.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        await db.SaveChangesAsync(ct);
        return Ok(program.ToDetailDto());
    }
}
