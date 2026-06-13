namespace Odisea.Application.Webhooks;

// The catalog of outbound webhook event names. A WebhookSubscription's EventTypes
// is a CSV of these. Keep in sync with frontend/components/EVENTS.md.
//
// All current events are agency-scoped (the dispatcher fans out to an agency's
// subscriptions). Operator-scoped events (e.g. offer.published) need an
// operator-subscription model and are a separate piece of work.
public static class WebhookEvents
{
    /// <summary>A traveler submitted a lead/booking request. Payload: LeadDto.</summary>
    public const string LeadCreated = "lead.created";

    /// <summary>An agency published (or re-published) a publication. Payload: PublicationDto.</summary>
    public const string PublicationPublished = "publication.published";

    public static readonly IReadOnlyList<string> All = [LeadCreated, PublicationPublished];
}
