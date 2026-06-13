namespace Odisea.Application.Webhooks;

// The actual outbound HTTP POST, abstracted so the dispatcher is unit-testable
// without a network. Infrastructure provides the HttpClient-backed implementation.
public interface IWebhookSender
{
    // Posts the signed body to url. Returns true on a 2xx, false on anything else
    // or a transport failure — never throws, so one dead endpoint can't break the
    // request that triggered the event.
    Task<bool> SendAsync(string url, string body, string signature, CancellationToken ct);
}
