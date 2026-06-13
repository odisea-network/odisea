namespace Odisea.Application.Webhooks;

// Fans an event out to an agency's active webhook subscriptions that want it.
// Fire-and-forget from the caller's perspective: failures are swallowed (a future
// slice adds a delivery log + retry), so emitting an event never fails the
// request that produced it.
public interface IWebhookDispatcher
{
    Task DispatchAsync(Guid agencyId, string eventType, object payload, CancellationToken ct);
}
