using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Common.Interfaces;
using Odisea.Application.Publications.Dtos;
using Odisea.Domain.Entities;

namespace Odisea.WebAPI.Controllers;

[ApiController]
[Route("api/v1/publications/{publicationId:guid}/domains")]
[Authorize(Policy = "AgencyAdmin")]
public class AllowedDomainsController(IAppDbContext db, IAgencyContext agencyCtx) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid publicationId, CancellationToken ct)
    {
        var pub = await LoadPublicationAsync(publicationId, ct);
        if (pub is null) return NotFound();
        if (Forbidden(pub)) return ForbiddenResult();

        return Ok(pub.AllowedDomains.Select(d => d.ToDto()));
    }

    /// <summary>
    /// Replaces the publication's allowed-domain list wholesale. An empty list
    /// opens the publication to any origin.
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> Replace(
        Guid publicationId, ManageDomainsRequest req, CancellationToken ct)
    {
        var pub = await LoadPublicationAsync(publicationId, ct);
        if (pub is null) return NotFound();
        if (Forbidden(pub)) return ForbiddenResult();

        var normalized = (req.Domains ?? [])
            .Select(d => d?.Trim().ToLowerInvariant())
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct()
            .ToList();

        var invalid = normalized.Where(d => !IsValidDomain(d!)).ToList();
        if (invalid.Count > 0)
            return Problem(title: "Validation",
                detail: $"Invalid domain(s): {string.Join(", ", invalid)}.", statusCode: 400);

        foreach (var existing in pub.AllowedDomains.ToList())
        {
            pub.AllowedDomains.Remove(existing);
            db.AllowedDomains.Remove(existing);
        }

        // Add via the DbSet (forces Added state for the client-generated key);
        // EF relationship fixup links each into pub.AllowedDomains for the response.
        foreach (var domain in normalized)
            db.AllowedDomains.Add(new AllowedDomain { PublicationId = pub.Id, Domain = domain! });

        pub.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(pub.AllowedDomains.Select(d => d.ToDto()));
    }

    private Task<Publication?> LoadPublicationAsync(Guid id, CancellationToken ct) =>
        db.Publications
            .Include(p => p.AllowedDomains)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    private bool Forbidden(Publication pub) =>
        agencyCtx.HasAgency && pub.AgencyId != agencyCtx.AgencyId;

    private IActionResult ForbiddenResult() =>
        Problem(title: "Forbidden", detail: "Publication does not belong to your agency.", statusCode: 403);

    // Host-only validation: a wildcard "*." prefix is allowed, no scheme/port/path.
    private static bool IsValidDomain(string domain)
    {
        var host = domain.StartsWith("*.", StringComparison.Ordinal) ? domain[2..] : domain;
        if (host.Length == 0 || host.Contains('/') || host.Contains(':')) return false;
        return Uri.CheckHostName(host) == UriHostNameType.Dns;
    }
}
