using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Odisea.Modules.Booking.Domain;
using Odisea.Modules.Booking.Infrastructure;
using Odisea.Modules.Integrations.PublicApi;
using Odisea.SharedKernel;

namespace Odisea.Modules.Booking.Features.Bookings;

/// Gives a supplier hold back for a booking whose closing status is already saved, and records
/// the outcome on the booking. Callers save the status first: a conflicting save then means
/// nothing was sent to the supplier, instead of a released hold behind a still-active booking.
public class ProviderReleaseService(
    BookingDbContext db, IProviderRegistry providers, IClock clock, ILogger<ProviderReleaseService> logger)
{
    public async Task ReleaseAsync(Domain.Booking booking, CancellationToken ct)
    {
        if (booking.ProviderRelease is not (ProviderReleaseStatus.Pending or ProviderReleaseStatus.Failed)
            || booking.ProviderBookingRef is not { } providerRef)
            return;

        try
        {
            var provider = providers.Get(booking.ProviderCode);
            var result = await provider.CancelAsync(providerRef, ct);
            if (result.Success)
            {
                booking.ProviderReleased(clock.UtcNow);
            }
            else if (await provider.GetBookingStateAsync(providerRef, ct) == ProviderBookingState.Cancelled)
            {
                // A refusal for a booking the supplier no longer holds (released earlier, or by
                // the supplier itself) is the outcome we wanted.
                booking.ProviderReleased(clock.UtcNow);
            }
            else
            {
                booking.ProviderReleaseRefused(
                    result.FailureReason is { Length: > 0 } reason ? reason : "The supplier refused without a reason.",
                    clock.UtcNow);
                logger.LogWarning("Supplier refused to release booking {BookingId}; an operator has to resolve it",
                    booking.Id);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Supplier boundary: any transport or adapter failure leaves the release pending for
            // a retry. The exception text can echo supplier payloads, so only its type is kept.
            booking.ProviderReleaseRetry($"The supplier could not be reached ({ex.GetType().Name}).", clock.UtcNow);
            logger.LogWarning(ex, "Releasing booking {BookingId} at the supplier failed; it stays pending for a retry",
                booking.Id);
        }

        try
        {
            // The supplier step already happened: record its outcome even if the caller went away.
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Someone else touched the booking meanwhile. The release stays pending in the
            // database, and the next attempt reconciles it through the supplier's booking state.
            logger.LogWarning("Could not record the supplier release of booking {BookingId}; it will be retried",
                booking.Id);
        }
    }
}
