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
        var supplierNames = await SupplierNamesFor(list, ct);
        return Ok(list.Select(o => o.ToDto(supplierNames)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        // Detail endpoint pulls the full pricing matrix; list stays light and only
        // exposes the "from" price (Offer.Price) — the components that read it
        // (od-offer-card / -grid) don't render the matrix anyway.
        var o = await db.Offers
            .Include(x => x.PriceVariants)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (o is null) return NotFound();
        var supplierNames = await SupplierNamesFor([o], ct);
        return Ok(o.ToDto(supplierNames));
    }

    // Resolve connection display names for the offers that carry source lineage,
    // so OfferDto.Source.Supplier is populated without a per-offer query.
    private async Task<IReadOnlyDictionary<Guid, string>> SupplierNamesFor(
        IReadOnlyCollection<Domain.Entities.Offer> offers, CancellationToken ct)
    {
        var ids = offers
            .Where(o => o.Source is not null)
            .Select(o => o.Source!.SupplierConnectionId)
            .Distinct()
            .ToList();

        if (ids.Count == 0) return new Dictionary<Guid, string>();

        return await db.SupplierConnections
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);
    }
}
