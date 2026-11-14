using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Odisea.Modules.Booking.Features.Maintenance;

/// Runs BookingMaintenanceRun on a timer. Single-instance by design (one API deployment per
/// operator); two instances would not corrupt anything — every write is guarded by the booking's
/// concurrency token — but could each send a payment reminder.
public sealed class BookingMaintenanceJob(
    IServiceScopeFactory scopeFactory,
    IOptions<BookingMaintenanceOptions> options,
    ILogger<BookingMaintenanceJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Booking maintenance disabled (Booking:Maintenance:Enabled=false)");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.Value.IntervalMinutes));
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<BookingMaintenanceRun>().RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Keep the job alive: a failed pass (database blip) is retried on the next tick.
                logger.LogError(ex, "Booking maintenance pass failed; retrying on the next tick");
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
