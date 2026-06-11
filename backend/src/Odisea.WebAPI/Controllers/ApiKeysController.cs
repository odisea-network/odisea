using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.ApiKeys.Dtos;
using Odisea.Application.Common.Interfaces;
using Odisea.Domain.Common;
using Odisea.Domain.Entities;

namespace Odisea.WebAPI.Controllers;

[ApiController]
[Route("api/v1/api-keys")]
[Authorize(Policy = "AgencyAdmin")]
public class ApiKeysController(IAppDbContext db, IAgencyContext agencyCtx) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!agencyCtx.HasAgency)
            return AgencyRequired();

        var agencyId = agencyCtx.RequireAgency();
        var keys = await db.ApiKeys
            .Where(k => k.AgencyId == agencyId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);

        return Ok(keys.Select(k => k.ToDto()));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateApiKeyRequest req, CancellationToken ct)
    {
        if (!agencyCtx.HasAgency)
            return AgencyRequired();

        if (string.IsNullOrWhiteSpace(req.Name))
            return Problem(title: "Validation", detail: "Name is required.", statusCode: 400);

        if (req.Scopes is null || req.Scopes.Length == 0)
            return Problem(title: "Validation", detail: "At least one scope is required.", statusCode: 400);

        var unknown = req.Scopes.Where(s => !ApiKeyScopes.All.Contains(s)).ToArray();
        if (unknown.Length > 0)
            return Problem(title: "Validation",
                detail: $"Unknown scope(s): {string.Join(", ", unknown)}.", statusCode: 400);

        if (req.ExpiresAt is { } exp && exp <= DateTime.UtcNow)
            return Problem(title: "Validation", detail: "ExpiresAt must be in the future.", statusCode: 400);

        var (rawKey, entity) = ApiKey.Generate(
            agencyCtx.RequireAgency(), req.Name.Trim(), req.Scopes, req.ExpiresAt);

        db.ApiKeys.Add(entity);
        await db.SaveChangesAsync(ct);

        // The raw key is returned here and never again.
        return CreatedAtAction(nameof(List), null, new CreateApiKeyResult(entity.ToDto(), rawKey));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        var key = await db.ApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (key is null) return NotFound();

        if (agencyCtx.AgencyId is Guid agencyId && key.AgencyId != agencyId)
            return Problem(title: "Forbidden", detail: "API key does not belong to your agency.", statusCode: 403);

        if (key.RevokedAt is null)
        {
            key.RevokedAt = DateTime.UtcNow;
            key.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return NoContent();
    }

    private IActionResult AgencyRequired() =>
        Problem(title: "Forbidden",
            detail: "API keys are scoped to an agency; operate within an agency context.",
            statusCode: 403);
}
