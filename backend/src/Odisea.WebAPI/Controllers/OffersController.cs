using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Catalog.Dtos;
using Odisea.Application.Common.Interfaces;
using Odisea.Domain.Enums;

namespace Odisea.WebAPI.Controllers;

[ApiController]
[Route("api/v1/offers")]
public class OffersController(IAppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(string? country, string? city, decimal? maxPrice, CancellationToken ct)
    {
        var q = db.Offers.Where(o => o.Status == OfferStatus.Published);
        if (!string.IsNullOrWhiteSpace(country)) q = q.Where(o => o.Country == country);
        if (!string.IsNullOrWhiteSpace(city)) q = q.Where(o => o.City == city);
        if (maxPrice is { } mp) q = q.Where(o => o.Price <= mp);
        var list = await q.OrderBy(o => o.Price).ToListAsync(ct);
        return Ok(list.Select(Mappings.ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var o = await db.Offers.FirstOrDefaultAsync(x => x.Id == id, ct);
        return o is null ? NotFound() : Ok(o.ToDto());
    }
}
