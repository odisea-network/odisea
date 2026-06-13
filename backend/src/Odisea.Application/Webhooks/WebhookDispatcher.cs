using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Common.Interfaces;
using Odisea.Domain.Enums;

namespace Odisea.Application.Webhooks;

public sealed class WebhookDispatcher(IAppDbContext db, IWebhookSender sender) : IWebhookDispatcher
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task DispatchAsync(Guid agencyId, string eventType, object payload, CancellationToken ct)
    {
        var subscriptions = await db.WebhookSubscriptions
            .Where(s => s.AgencyId == agencyId && s.Status == WebhookStatus.Active)
            .ToListAsync(ct);

        // WantsEvent is a CSV check — do it in memory rather than translating to SQL.
        var targets = subscriptions.Where(s => s.WantsEvent(eventType)).ToList();
        if (targets.Count == 0) return;

        var envelope = new WebhookEnvelope(eventType, DateTime.UtcNow, payload);
        var body = JsonSerializer.Serialize(envelope, Json);

        foreach (var sub in targets)
        {
            var signature = Sign(body, sub.Secret);
            // Swallow per-target failures: a dead endpoint (or a misbehaving sender)
            // must not affect the other targets or the originating request.
            try
            {
                await sender.SendAsync(sub.Url, body, signature, ct);
            }
            catch
            {
                // Intentionally ignored — delivery is best-effort in this slice;
                // a delivery log + retry is a follow-up.
            }
        }
    }

    // Hex HMAC-SHA256 of the raw body, the value the receiver recomputes with its
    // copy of the secret to authenticate the call.
    public static string Sign(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed record WebhookEnvelope(string Event, DateTime OccurredAt, object Data);
}
