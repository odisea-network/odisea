using System.Diagnostics;
using Odisea.Modules.Integrations.Domain;
using Odisea.Modules.Integrations.Infrastructure;
using Odisea.Modules.Integrations.PublicApi;

namespace Odisea.Modules.Integrations.Features.CallLogging;

/// Wraps every adapter so each outbound call leaves an auditable, PII-free trace.
public class LoggingReservationProvider(IReservationProvider inner, IntegrationsDbContext db) : IReservationProvider
{
    public string ProviderCode => inner.ProviderCode;

    public Task<ProviderSearchResult> SearchAsync(ProviderSearchRequest request, CancellationToken ct) =>
        LogAsync("Search", ct,
            () => inner.SearchAsync(request, ct),
            r => $"location={request.ExternalLocationCode} hotels={request.ExternalHotelCodes.Count} offers={r.Offers.Count}");

    public Task<ProviderRePriceResult> RePriceAsync(string offerToken, CancellationToken ct) =>
        LogAsync("RePrice", ct,
            () => inner.RePriceAsync(offerToken, ct),
            r => $"available={r.IsAvailable} price={r.CurrentNetPrice?.ToString() ?? "-"}");

    public Task<ProviderBookingResult> BookAsync(ProviderBookingRequest request, CancellationToken ct) =>
        LogAsync("Book", ct,
            () => inner.BookAsync(request, ct),
            r => $"pax={request.Passengers.Count} success={r.Success} ref={r.ProviderBookingRef ?? "-"} reason={r.FailureReason ?? "-"}");

    public Task<ProviderCancelResult> CancelAsync(string providerBookingRef, CancellationToken ct) =>
        LogAsync("Cancel", ct,
            () => inner.CancelAsync(providerBookingRef, ct),
            r => $"ref={providerBookingRef} success={r.Success}");

    public Task<ProviderBookingState> GetBookingStateAsync(string providerBookingRef, CancellationToken ct) =>
        LogAsync("Status", ct,
            () => inner.GetBookingStateAsync(providerBookingRef, ct),
            r => $"ref={providerBookingRef} state={r}");

    private async Task<T> LogAsync<T>(
        string operation, CancellationToken ct, Func<Task<T>> call, Func<T, string> summarize)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await call();
            await WriteAsync(operation, stopwatch.ElapsedMilliseconds, true, Truncate(summarize(result)), ct);
            return result;
        }
        catch (Exception ex)
        {
            await WriteAsync(operation, stopwatch.ElapsedMilliseconds, false, Truncate(ex.GetType().Name), ct);
            throw;
        }
    }

    private async Task WriteAsync(string operation, long ms, bool success, string detail, CancellationToken ct)
    {
        db.ProviderCallLogs.Add(new ProviderCallLog
        {
            ProviderCode = inner.ProviderCode,
            Operation = operation,
            DurationMs = ms,
            Success = success,
            Detail = detail,
        });
        await db.SaveChangesAsync(ct);
    }

    private static string Truncate(string value) => value.Length <= 500 ? value : value[..500];
}
