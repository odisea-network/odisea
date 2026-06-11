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

        try { FilterResolver.Apply(db.Offers.AsQueryable(), req.Filter); }
        catch (FilterValidationException ex)
        {
            return Problem(title: "Invalid filter", detail: ex.Message, statusCode: 400);
        }

        // Slugs are unique per agency (#18): a clash is only a conflict within the
        // caller's own agency. Another agency owning the same slug is fine.
        var slugTaken = await db.Collections
            .AnyAsync(c => c.AgencyId == agencyCtx.AgencyId && c.Slug == req.Slug, ct);
        if (slugTaken)
            return Problem(title: "Slug already in use",
                detail: $"Your agency already has a collection with slug '{req.Slug}'.",
                statusCode: 409);

        var entity = new Collection
        {
            AgencyId = agencyCtx.AgencyId,
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

    private Task<Collection?> FindAsync(string idOrSlug, CancellationToken ct)
    {
        // Ids are global; resolve them directly.
        if (Guid.TryParse(idOrSlug, out var id))
            return db.Collections.FirstOrDefaultAsync(c => c.Id == id, ct);

        // A slug is unique only within an agency (#18), so resolving one requires
        // agency context. The public embed resolves offers by collection id (see
        // PublicationResolver), so anonymous slug lookups are intentionally unsupported.
        if (!agencyCtx.HasAgency)
            return Task.FromResult<Collection?>(null);

        var agencyId = agencyCtx.AgencyId;
        return db.Collections.FirstOrDefaultAsync(
            c => c.Slug == idOrSlug && c.AgencyId == agencyId, ct);
    }
}
