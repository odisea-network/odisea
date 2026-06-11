using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Common.Interfaces;
using Odisea.Application.Experiences.Dtos;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;

namespace Odisea.WebAPI.Controllers;

[ApiController]
[Route("api/v1/experiences")]
public class ExperiencesController(IAppDbContext db, IAgencyContext agencyCtx) : ControllerBase
{
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var q = db.Experiences.AsQueryable();
        if (agencyCtx.AgencyId is Guid agencyId)
            q = q.Where(e => e.AgencyId == agencyId);

        var list = await q.OrderBy(e => e.Name).ToListAsync(ct);
        return Ok(list.Select(ExperienceMappings.ToDto));
    }

    [Authorize]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var experience = await db.Experiences.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (experience is null) return NotFound();

        if (agencyCtx.AgencyId is Guid agencyId && experience.AgencyId != agencyId)
            return Problem(title: "Forbidden", detail: "Experience does not belong to your agency.", statusCode: 403);

        return Ok(experience.ToDto());
    }

    [Authorize(Policy = "AgencyMember")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateExperienceRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return Problem(title: "Validation", detail: "Name is required", statusCode: 400);

        if (agencyCtx.AgencyId is not Guid agencyId)
            return Problem(title: "Validation",
                detail: "Platform admins must specify an agency; this endpoint requires an agency-scoped caller.",
                statusCode: 400);

        var experience = new Experience
        {
            AgencyId = agencyId,
            Name     = req.Name,
            Status   = ExperienceStatus.Draft,
            Version  = 1,
            Config   = req.Config ?? new Domain.ValueObjects.ExperienceConfig(),
        };

        db.Experiences.Add(experience);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = experience.Id }, experience.ToDto());
    }

    [Authorize(Policy = "AgencyMember")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateExperienceRequest req, CancellationToken ct)
    {
        var experience = await db.Experiences.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (experience is null) return NotFound();

        if (agencyCtx.AgencyId is Guid agencyId && experience.AgencyId != agencyId)
            return Problem(title: "Forbidden", detail: "Experience does not belong to your agency.", statusCode: 403);

        if (experience.Status == ExperienceStatus.Published)
            return Problem(title: "Conflict", detail: "Published experiences cannot be edited; create a new draft.", statusCode: 409);

        if (req.Name is not null)
            experience.Name = req.Name;

        if (req.Config is not null)
            experience.Config = req.Config;

        experience.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(experience.ToDto());
    }

    [Authorize(Policy = "AgencyAdmin")]
    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        var experience = await db.Experiences.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (experience is null) return NotFound();

        if (agencyCtx.AgencyId is Guid agencyId && experience.AgencyId != agencyId)
            return Problem(title: "Forbidden", detail: "Experience does not belong to your agency.", statusCode: 403);

        if (experience.Status == ExperienceStatus.Published)
            return Problem(title: "Conflict", detail: "Experience is already published.", statusCode: 409);

        experience.Status    = ExperienceStatus.Published;
        experience.Version   += 1;
        experience.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(experience.ToDto());
    }
}
