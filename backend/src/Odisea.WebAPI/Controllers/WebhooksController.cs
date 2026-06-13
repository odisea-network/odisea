using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Common.Interfaces;
using Odisea.Application.Webhooks.Dtos;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;

namespace Odisea.WebAPI.Controllers;

// Agency-managed outbound webhook subscriptions (CRM integrations). The signing
// secret is generated server-side and returned only at creation.
[ApiController]
[Route("api/v1/webhooks")]
[Authorize(Policy = "AgencyAdmin")]
public class WebhooksController(IAppDbContext db, IAgencyContext agencyCtx) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var agencyId = agencyCtx.RequireAgency();
        var list = await db.WebhookSubscriptions
            .Where(s => s.AgencyId == agencyId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);
        return Ok(list.Select(s => s.ToDto()));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateWebhookSubscriptionRequest req, CancellationToken ct)
    {
        var agencyId = agencyCtx.RequireAgency();

        if (string.IsNullOrWhiteSpace(req.Url) || !Uri.TryCreate(req.Url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return Problem(title: "Validation", detail: "A valid absolute http(s) URL is required.", statusCode: 400);

        if (string.IsNullOrWhiteSpace(req.EventTypes))
            return Problem(title: "Validation", detail: "At least one event type is required.", statusCode: 400);

        var secret = GenerateSecret();
        var sub = new WebhookSubscription
        {
            AgencyId = agencyId,
            Url = req.Url,
            EventTypes = req.EventTypes,
            Secret = secret,
            Status = WebhookStatus.Active,
        };
        db.WebhookSubscriptions.Add(sub);
        await db.SaveChangesAsync(ct);

        // Secret returned exactly once — the agency stores it to verify signatures.
        return CreatedAtAction(nameof(List), null,
            new CreatedWebhookSubscriptionDto(sub.Id, sub.Url, sub.EventTypes, secret));
    }

    [HttpPost("{id:guid}/disable")]
    public Task<IActionResult> Disable(Guid id, CancellationToken ct) => SetStatus(id, WebhookStatus.Disabled, ct);

    [HttpPost("{id:guid}/enable")]
    public Task<IActionResult> Enable(Guid id, CancellationToken ct) => SetStatus(id, WebhookStatus.Active, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var sub = await FindOwned(id, ct);
        if (sub is null) return NotFound();
        db.WebhookSubscriptions.Remove(sub);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<IActionResult> SetStatus(Guid id, WebhookStatus status, CancellationToken ct)
    {
        var sub = await FindOwned(id, ct);
        if (sub is null) return NotFound();
        sub.Status = status;
        sub.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(sub.ToDto());
    }

    private async Task<WebhookSubscription?> FindOwned(Guid id, CancellationToken ct)
    {
        var agencyId = agencyCtx.RequireAgency();
        var sub = await db.WebhookSubscriptions.FirstOrDefaultAsync(s => s.Id == id, ct);
        return sub is not null && sub.AgencyId == agencyId ? sub : null;
    }

    // 32 bytes of base64url entropy — plenty for HMAC-SHA256 keying.
    private static string GenerateSecret()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
