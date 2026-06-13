using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Common.Interfaces;
using Odisea.Application.Themes;
using Odisea.Application.Themes.Dtos;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;

namespace Odisea.WebAPI.Controllers;

[ApiController]
[Route("api/v1/themes")]
public class ThemesController(IAppDbContext db, IAgencyContext agencyCtx) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? agencyId, CancellationToken ct)
    {
        // Presets are platform templates with their own endpoint — keep them out of
        // an agency's working theme list.
        var q = db.Themes.Where(t => !t.IsPreset);
        if (agencyId.HasValue)
            q = q.Where(t => t.AgencyId == agencyId.Value);

        var list = await q.OrderBy(t => t.Name).ToListAsync(ct);
        return Ok(list.Select(ThemeMappings.ToDto));
    }

    // The theme marketplace: platform-owned preset templates any agency can browse.
    // Anonymous — presets are public catalog content, not tenant data.
    [HttpGet("presets")]
    public async Task<IActionResult> Presets(CancellationToken ct)
    {
        var presets = await db.Themes
            .Where(t => t.IsPreset)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);
        return Ok(presets.Select(ThemeMappings.ToDto));
    }

    // Clones a preset's tokens into a new Draft theme owned by the caller's agency.
    [Authorize(Policy = "AgencyMember")]
    [HttpPost("from-preset/{presetId:guid}")]
    public async Task<IActionResult> CloneFromPreset(Guid presetId, CloneFromPresetRequest req, CancellationToken ct)
    {
        if (agencyCtx.AgencyId is not Guid agencyId)
            return Problem(title: "Validation",
                detail: "Platform admins must specify an agency; this endpoint requires an agency-scoped caller.",
                statusCode: 400);

        var preset = await db.Themes.FirstOrDefaultAsync(t => t.Id == presetId && t.IsPreset, ct);
        if (preset is null)
            return Problem(title: "Not found", detail: "Preset not found.", statusCode: 404);

        var theme = new Theme
        {
            AgencyId = agencyId,
            Name = string.IsNullOrWhiteSpace(req.Name) ? $"{preset.Name} (copy)" : req.Name,
            Status = ThemeStatus.Draft,
            Version = 1,
            // Deep-copy the tokens so editing the clone never touches the preset.
            Tokens = CloneTokens(preset.Tokens),
            IsPreset = false,
        };

        db.Themes.Add(theme);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = theme.Id }, theme.ToDto());
    }

    private static Domain.ValueObjects.ThemeTokens CloneTokens(Domain.ValueObjects.ThemeTokens src) => new()
    {
        Foundation = new(src.Foundation),
        Semantic = new(src.Semantic),
        Component = new(src.Component),
    };

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var theme = await db.Themes.FirstOrDefaultAsync(t => t.Id == id, ct);
        return theme is null ? NotFound() : Ok(theme.ToDto());
    }

    [Authorize(Policy = "AgencyMember")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateThemeRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return Problem(title: "Validation", detail: "Name is required", statusCode: 400);

        if (agencyCtx.AgencyId is not Guid agencyId)
            return Problem(title: "Validation",
                detail: "Platform admins must specify an agency; this endpoint requires an agency-scoped caller.",
                statusCode: 400);

        var theme = new Theme
        {
            AgencyId = agencyId,
            Name     = req.Name,
            Status   = ThemeStatus.Draft,
            Version  = 1,
            Tokens   = req.Tokens ?? Domain.ValueObjects.ThemeTokens.Default(),
        };

        db.Themes.Add(theme);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = theme.Id }, theme.ToDto());
    }

    [Authorize(Policy = "AgencyMember")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateThemeRequest req, CancellationToken ct)
    {
        var theme = await db.Themes.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (theme is null) return NotFound();

        if (agencyCtx.AgencyId is Guid agencyId && theme.AgencyId != agencyId)
            return Problem(title: "Forbidden", detail: "Theme does not belong to your agency.", statusCode: 403);

        if (theme.Status == ThemeStatus.Published)
            return Problem(title: "Conflict", detail: "Published themes cannot be edited; create a new draft.", statusCode: 409);

        if (req.Name is not null)
            theme.Name = req.Name;

        if (req.Tokens is not null)
            theme.Tokens = req.Tokens;

        theme.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(theme.ToDto());
    }

    [Authorize(Policy = "AgencyAdmin")]
    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        var theme = await db.Themes.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (theme is null) return NotFound();

        if (agencyCtx.AgencyId is Guid agencyId && theme.AgencyId != agencyId)
            return Problem(title: "Forbidden", detail: "Theme does not belong to your agency.", statusCode: 403);

        if (theme.Status == ThemeStatus.Published)
            return Problem(title: "Conflict", detail: "Theme is already published.", statusCode: 409);

        theme.Status    = ThemeStatus.Published;
        theme.Version   += 1;
        theme.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(theme.ToDto());
    }

    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, [FromQuery] string format = "css", CancellationToken ct = default)
    {
        var theme = await db.Themes.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (theme is null) return NotFound();

        return format.ToLowerInvariant() switch
        {
            "css"  => Content(ThemeExporter.ExportCss(theme),  "text/css"),
            "json" => Content(ThemeExporter.ExportJson(theme), "application/json"),
            _      => Problem(title: "Validation", detail: "format must be 'css' or 'json'", statusCode: 400),
        };
    }
}
