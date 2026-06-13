using Odisea.Domain.Common;
using Odisea.Domain.Enums;

namespace Odisea.Domain.Entities;

// An agency's outbound webhook: when a matching event fires (currently
// "lead.created"), Odisea POSTs a signed JSON payload to Url. The Secret is used
// to HMAC-sign the body so the receiver can verify the call came from us.
//
// First slice supports the lead.created event only; EventTypes is a CSV so more
// event names can be added without a schema change.
public class WebhookSubscription : Entity
{
    public Guid AgencyId { get; set; }

    public string Url { get; set; } = string.Empty;

    // Shared secret for HMAC-SHA256 body signing. Sent once at creation, then
    // stored; the receiver keeps its own copy to verify the X-Odisea-Signature.
    public string Secret { get; set; } = string.Empty;

    // Comma-separated event names this subscription wants, e.g. "lead.created".
    public string EventTypes { get; set; } = string.Empty;

    public WebhookStatus Status { get; set; } = WebhookStatus.Active;

    public bool WantsEvent(string eventType) =>
        EventTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(eventType, StringComparer.OrdinalIgnoreCase);
}
