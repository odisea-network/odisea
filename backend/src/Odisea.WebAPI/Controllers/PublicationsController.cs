using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Common.Interfaces;
using Odisea.Application.Publications;
using Odisea.Application.Publications.Dtos;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;

namespace Odisea.WebAPI.Controllers;

[ApiController]
[Route("api/v1/publications")]
public class PublicationsController(IAppDbContext db, IAgencyContext agencyCtx) : ControllerBase
{
    // ── Public manifest ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the cacheable manifest for an embed key.
    /// Checks the Origin header against AllowedDomains when the list is non-empty.
    /// </summary>
    [EnableCors("PublicEmbedCors")]
    [HttpGet("{key}")]
    public async Task<IActionResult> GetManifest(string key, CancellationToken ct)
    {
        // Route constraint: if the segment parses as a GUID, let {id:guid} routes handle it.
        // This action is the fallback for short key strings.
        if (Guid.TryParse(key, out _))
            return NotFound();

        var pub = await db.Publications
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Key == key, ct);

        if (pub is null) return NotFound();

        // Origin allowlisting is enforced upstream by EmbedSecurityMiddleware.

        PublicationManifestDto manifest;
        try
        {
            manifest = await PublicationResolver.ResolveManifestAsync(pub, db, ct);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(title: "Manifest error", detail: ex.Message, statusCode: 500);
        }

        Response.Headers.ETag = manifest.ETag;
        Response.Headers.CacheControl = pub.Status == PublicationStatus.Published
            ? "public, max-age=60, stale-while-revalidate=300"
            : "no-store";

        return Ok(manifest);
    }

    // ── Management CRUD ────────────────────────────────────────────────────────

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var q = db.Publications.Include(p => p.AllowedDomains).AsQueryable();
        if (agencyCtx.HasAgency)
            q = q.Where(p => p.AgencyId == agencyCtx.AgencyId);

        var list = await q.OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
        return Ok(list.Select(p => p.ToDto()));
    }

    [Authorize]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var pub = await db.Publications
            .Include(p => p.AllowedDomains)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (pub is null) return NotFound();

        if (agencyCtx.HasAgency && pub.AgencyId != agencyCtx.AgencyId)
            return Problem(title: "Forbidden", detail: "Publication does not belong to your agency.", statusCode: 403);

        return Ok(pub.ToDto());
    }

    [Authorize(Policy = "AgencyAdmin")]
    [HttpPost]
    public async Task<IActionResult> Create(CreatePublicationRequest req, CancellationToken ct)
    {
        if (req.CollectionId == Guid.Empty)
            return Problem(title: "Validation", detail: "CollectionId is required.", statusCode: 400);

        var collectionExists = await db.Collections.AnyAsync(c => c.Id == req.CollectionId, ct);
        if (!collectionExists)
            return Problem(title: "Validation", detail: $"Collection {req.CollectionId} not found.", statusCode: 400);

        var pub = new Publication
        {
            AgencyId = agencyCtx.AgencyId,
            CollectionId = req.CollectionId,
            ThemeId = req.ThemeId,
            ExperienceId = req.ExperienceId,
            ExperienceConfig = req.ExperienceConfig,
            AllowedDomains = [.. (req.AllowedDomains ?? [])
                .Select(d => new AllowedDomain { Domain = d })],
            Status = PublicationStatus.Draft,
            Version = 0,
        };

        db.Publications.Add(pub);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = pub.Id }, pub.ToDto());
    }

    [Authorize(Policy = "AgencyMember")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdatePublicationRequest req, CancellationToken ct)
    {
        var pub = await db.Publications
            .Include(p => p.AllowedDomains)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (pub is null) return NotFound();

        if (agencyCtx.HasAgency && pub.AgencyId != agencyCtx.AgencyId)
            return Problem(title: "Forbidden", detail: "Publication does not belong to your agency.", statusCode: 403);

        if (req.CollectionId.HasValue)
        {
            var collectionExists = await db.Collections.AnyAsync(c => c.Id == req.CollectionId.Value, ct);
            if (!collectionExists)
                return Problem(title: "Validation", detail: $"Collection {req.CollectionId} not found.", statusCode: 400);
            pub.CollectionId = req.CollectionId.Value;
        }

        if (req.ThemeId.HasValue) pub.ThemeId = req.ThemeId;
        if (req.ExperienceId.HasValue) pub.ExperienceId = req.ExperienceId;
        if (req.ExperienceConfig is not null) pub.ExperienceConfig = req.ExperienceConfig;

        if (req.AllowedDomains is not null)
        {
            foreach (var existing in pub.AllowedDomains.ToList())
            {
                pub.AllowedDomains.Remove(existing);
                db.AllowedDomains.Remove(existing);
            }
            foreach (var domain in req.AllowedDomains)
                db.AllowedDomains.Add(new AllowedDomain { PublicationId = pub.Id, Domain = domain });
        }

        pub.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(pub.ToDto());
    }

    [Authorize(Policy = "AgencyAdmin")]
    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        var pub = await db.Publications
            .Include(p => p.AllowedDomains)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (pub is null) return NotFound();

        if (agencyCtx.HasAgency && pub.AgencyId != agencyCtx.AgencyId)
            return Problem(title: "Forbidden", detail: "Publication does not belong to your agency.", statusCode: 403);

        if (pub.Status == PublicationStatus.Published)
            return Ok(pub.ToDto());

        pub.Status = PublicationStatus.Published;
        pub.Version += 1;
        pub.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(pub.ToDto());
    }
}
