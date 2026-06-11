using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Catalog.Collections;
using Odisea.Application.Catalog.Dtos;
using Odisea.Application.Catalog.Filtering;
using Odisea.Application.Common.Interfaces;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Domain.ValueObjects;

namespace Odisea.WebAPI.Controllers;

[ApiController]
[Route("api/v1/collections")]
public class CollectionsController(IAppDbContext db, IAgencyContext agencyCtx) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var list = await db.Collections.OrderBy(c => c.Name).ToListAsync(ct);
        return Ok(list.Select(Mappings.ToDto));
    }

    [HttpGet("{idOrSlug}")]
    public async Task<IActionResult> Get(string idOrSlug, CancellationToken ct)
    {
        var c = await FindAsync(idOrSlug, ct);
        return c is null ? NotFound() : Ok(c.ToDto());
    }

    [EnableCors("PublicEmbedCors")]
    [HttpGet("{idOrSlug}/offers")]
    public async Task<IActionResult> Resolve(string idOrSlug, CancellationToken ct)
    {
        var c = await FindAsync(idOrSlug, ct);
        if (c is null) return NotFound();
        try
        {
            var offers = await CollectionResolver.ResolveAsync(c, db.Offers.AsQueryable(), ct);
            return Ok(offers.Select(Mappings.ToDto));
        }
        catch (FilterValidationException ex)
        {
            return Problem(title: "Invalid filter", detail: ex.Message, statusCode: 400);
        }
    }

    [Authorize(Policy = "AgencyMember")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateCollectionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Slug))
            return Problem(title: "Validation", detail: "Name and Slug are required", statusCode: 400);

        if (agencyCtx.AgencyId is not Guid agencyId)
            return Problem(title: "Validation",
                detail: "Platform admins must specify an agency; this endpoint requires an agency-scoped caller.",
                statusCode: 400);

        try { FilterResolver.Apply(db.Offers.AsQueryable(), req.Filter); }
        catch (FilterValidationException ex)
        {
            return Problem(title: "Invalid filter", detail: ex.Message, statusCode: 400);
        }

        var entity = new Collection
        {
            AgencyId = agencyId,
            Name = req.Name,
            Slug = req.Slug,
            Status = CollectionStatus.Draft,
            Filter = req.Filter,
            Sort = req.Sort ?? new SortSpec("price", "asc"),
            PinnedOfferIds = req.PinnedOfferIds ?? new(),
            ExcludedOfferIds = req.ExcludedOfferIds ?? new(),
        };
        db.Collections.Add(entity);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { idOrSlug = entity.Id.ToString() }, entity.ToDto());
    }

    private Task<Collection?> FindAsync(string idOrSlug, CancellationToken ct) =>
        Guid.TryParse(idOrSlug, out var id)
            ? db.Collections.FirstOrDefaultAsync(c => c.Id == id, ct)
            : db.Collections.FirstOrDefaultAsync(c => c.Slug == idOrSlug, ct);
}
