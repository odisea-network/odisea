using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Analytics;
using Odisea.Application.Analytics.Dtos;
using Odisea.Application.Common.Interfaces;

namespace Odisea.WebAPI.Controllers;

[ApiController]
[Route("api/v1/analytics")]
[Authorize(Policy = "AgencyMember")]
public class AnalyticsController(IAppDbContext db, IAgencyContext agencyCtx) : ControllerBase
{
    /// <summary>
    /// Returns aggregated stats for a publication over the requested window.
    /// Only the owning agency (or a platform admin) can read its stats.
    /// </summary>
    [HttpGet("publications/{key}")]
    public async Task<IActionResult> GetPublicationStats(
        string key,
        [FromQuery] string? window,
        CancellationToken ct)
    {
        if (!AnalyticsWindowExtensions.TryParse(window, out var parsed) && window is not null)
            return Problem(title: "Validation", detail: "window must be one of: 24h, 7d, 30d.", statusCode: 400);

        var pub = await db.Publications
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Key == key, ct);

        if (pub is null) return NotFound();

        if (agencyCtx.AgencyId is Guid agencyId && pub.AgencyId != agencyId)
            return Problem(title: "Forbidden", detail: "Publication does not belong to your agency.", statusCode: 403);

        var stats = await AnalyticsQuery.GetStatsAsync(key, parsed, db, ct);
        return Ok(stats);
    }
}
