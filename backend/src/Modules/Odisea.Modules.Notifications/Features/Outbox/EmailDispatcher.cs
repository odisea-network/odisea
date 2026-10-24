using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Odisea.Modules.Notifications.Domain;
using Odisea.Modules.Notifications.Infrastructure;
using Odisea.SharedKernel;

namespace Odisea.Modules.Notifications.Features.Outbox;

public sealed record DispatchResult(int Processed, int Sent, int Failed);

/// One pass over the due part of the outbox. Scoped (owns a DbContext); the hosted dispatcher
/// and the operator "dispatch now" endpoint both run it.
public sealed class EmailDispatchRun(
    NotificationsDbContext db,
    IEmailTransport transport,
    IOptions<EmailOptions> options,
    IClock clock,
    ILogger<EmailDispatchRun> logger)
{
    public async Task<DispatchResult> RunAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var due = await db.EmailMessages
            .Where(m => m.Status == EmailStatus.Pending && m.NextAttemptAt <= now)
            .OrderBy(m => m.NextAttemptAt)
            .Take(options.Value.BatchSize)
            .ToListAsync(ct);

        var sent = 0;
        var failed = 0;
        foreach (var message in due)
        {
            try
            {
                await transport.SendAsync(message, ct);
                message.MarkSent(clock.UtcNow);
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.MarkFailed(ex.Message, clock.UtcNow, options.Value.MaxAttempts);
                failed++;
                logger.LogWarning(ex, "E-mail {EmailId} ({Category}) failed on attempt {Attempt} via {Transport}; status {Status}",
                    message.Id, message.Category, message.Attempts, transport.Name, message.Status);
            }
            // One message per save: a crash mid-batch loses at most the message in flight.
            await db.SaveChangesAsync(ct);
        }
        return new DispatchResult(due.Count, sent, failed);
    }
}

/// Polls the outbox. Single-instance by design (one API deployment per operator); a second
/// instance would need row claiming before this can scale out.
public sealed class EmailDispatcher(
    IServiceScopeFactory scopeFactory,
    IOptions<EmailOptions> options,
    ILogger<EmailDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("E-mail dispatcher disabled (Email:Enabled=false); the outbox will only fill");
            return;
        }

        var interval = TimeSpan.FromSeconds(options.Value.PollSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            var drained = true;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<EmailDispatchRun>().RunAsync(stoppingToken);
                drained = result.Processed < options.Value.BatchSize;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "E-mail dispatch pass failed; retrying after the poll interval");
            }

            if (!drained)
                continue;
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
