using Odisea.Modules.Integrations.PublicApi;

namespace Odisea.UnitTests.Booking;

/// The mock supplier with scriptable cancel, booking-state and mid-booking hooks, for the tests
/// of when and how a supplier hold is given back.
internal sealed class ScriptedProvider(IReservationProvider inner) : IReservationProvider
{
    /// Replaces the supplier's answer to a cancel; null keeps the mock's (always succeeds).
    public Func<string, Task<ProviderCancelResult>>? OnCancel { get; set; }

    /// What the supplier reports for a booking it refused to cancel.
    public ProviderBookingState StateAfterRefusal { get; set; } = ProviderBookingState.Confirmed;

    /// Runs after the supplier accepted a booking and before the answer reaches Odisea.
    public Func<Task>? DuringBook { get; set; }

    public List<string> CancelCalls { get; } = [];

    public string ProviderCode => inner.ProviderCode;

    public Task<ProviderSearchResult> SearchAsync(ProviderSearchRequest request, CancellationToken ct) =>
        inner.SearchAsync(request, ct);

    public Task<ProviderRePriceResult> RePriceAsync(string offerToken, CancellationToken ct) =>
        inner.RePriceAsync(offerToken, ct);

    public async Task<ProviderBookingResult> BookAsync(ProviderBookingRequest request, CancellationToken ct)
    {
        var result = await inner.BookAsync(request, ct);
        if (DuringBook is { } hook)
            await hook();
        return result;
    }

    public async Task<ProviderCancelResult> CancelAsync(string providerBookingRef, CancellationToken ct)
    {
        CancelCalls.Add(providerBookingRef);
        return OnCancel is { } scripted ? await scripted(providerBookingRef) : await inner.CancelAsync(providerBookingRef, ct);
    }

    public Task<ProviderBookingState> GetBookingStateAsync(string providerBookingRef, CancellationToken ct) =>
        Task.FromResult(StateAfterRefusal);
}
