using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Odisea.Application.Analytics;
using Odisea.Application.Analytics.Dtos;
using Odisea.Application.Common.Interfaces;

namespace Odisea.WebAPI.Controllers;

[ApiController]
[Route("api/v1/events")]
public class EventsController(IAppDbContext db) : ControllerBase
{
    /// <summary>
    /// Ingests a batch of analytics events. Anonymous + IP rate-limited; API-key
    /// auth lands with #27. Always returns 202 — events for unknown publication
    /// keys are dropped silently so the response never reveals which keys exist.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting("events")]
    [HttpPost]
    public async Task<IActionResult> Ingest(IngestEventCommand command, CancellationToken ct)
    {
        if (command?.Events is null || command.Events.Count == 0)
            return Accepted();

        var ipHash = Hash(HttpContext.Connection.RemoteIpAddress?.ToString());
        var uaHash = Hash(Request.Headers.UserAgent.ToString());

        await EventIngestor.IngestAsync(command, ipHash, uaHash, db, ct);
        return Accepted();
    }

    // SHA-256 hex. Raw IP / user-agent are never persisted — privacy by construction.
    private static string? Hash(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
