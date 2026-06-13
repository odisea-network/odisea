using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging;
using Odisea.Application.Webhooks;

namespace Odisea.Infrastructure.Webhooks;

public sealed class HttpWebhookSender(HttpClient http, ILogger<HttpWebhookSender> logger) : IWebhookSender
{
    public async Task<bool> SendAsync(string url, string body, string signature, CancellationToken ct)
    {
        try
        {
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            content.Headers.Add("X-Odisea-Signature", $"sha256={signature}");

            using var response = await http.PostAsync(url, content, ct);
            if (response.IsSuccessStatusCode) return true;

            logger.LogWarning("Webhook to {Url} returned {Status}", url, (int)response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            // A bad URL or a down receiver is a delivery failure, not an app error.
            logger.LogWarning(ex, "Webhook delivery to {Url} failed", url);
            return false;
        }
    }
}
