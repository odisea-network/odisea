using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Odisea.Application.Common.Interfaces;
using Odisea.Application.Suppliers.Connectors;
using Odisea.Application.Suppliers.Freshness;
using Odisea.Domain.Enums;

namespace Odisea.Infrastructure.Suppliers.Connectors;

// Periodically syncs active supplier connections that have gone stale: runs the
// connector (recording an ImportJob) then sweeps the connection for offers that
// aged past their freshness TTL. Each connection is isolated — a thrown import
// fails just that connection's tick, not the loop.
public sealed class ConnectorSchedulerService(
    IServiceProvider services,
    ConnectorSchedulerOptions options,
    ILogger<ConnectorSchedulerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Connector scheduler started (poll every {Poll}, resync after {Sync}).",
            options.PollInterval, options.SyncInterval);

        using var timer = new PeriodicTimer(options.PollInterval);
        do
        {
            try
            {
                await RunDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A failure building the batch (e.g. DB unreachable) must not kill the
                // loop — log and try again on the next tick.
                logger.LogError(ex, "Connector scheduler tick failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunDueAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var runner = scope.ServiceProvider.GetRequiredService<IImportRunner>();
        var freshness = scope.ServiceProvider.GetRequiredService<IFreshnessService>();

        var now = DateTime.UtcNow;
        var active = await db.SupplierConnections
            .Where(c => c.Status == SupplierConnectionStatus.Active)
            .ToListAsync(ct);

        var due = ConnectorScheduleEvaluator
            .DueConnections(active, now, options.SyncInterval)
            .ToList();

        if (due.Count == 0) return;

        logger.LogInformation("Connector scheduler syncing {Count} due connection(s).", due.Count);

        foreach (var connection in due)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var job = await runner.RunAsync(connection, ct);
                await freshness.SweepAsync(connection.Id, ct);
                logger.LogInformation(
                    "Synced connection {Connection}: job {Job} {Status} ({Imported} imported).",
                    connection.Name, job.Id, job.Status, job.OffersImported);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled sync failed for connection {Connection}.", connection.Name);
            }
        }
    }
}
