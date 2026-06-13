using Odisea.Domain.Entities;

namespace Odisea.Application.Webhooks.Dtos;

// The secret is returned ONCE, at creation, so the agency can configure its
// receiver. Subsequent reads never echo it back.
public record WebhookSubscriptionDto(
    Guid Id,
    Guid AgencyId,
    string Url,
    string EventTypes,
    string Status,
    DateTime CreatedAt
);

public record CreatedWebhookSubscriptionDto(
    Guid Id,
    string Url,
    string EventTypes,
    string Secret
);

public record CreateWebhookSubscriptionRequest(
    string Url,
    string EventTypes
);

public static class WebhookMappings
{
    public static WebhookSubscriptionDto ToDto(this WebhookSubscription s) => new(
        s.Id, s.AgencyId, s.Url, s.EventTypes, s.Status.ToString(), s.CreatedAt);
}
