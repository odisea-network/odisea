using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Odisea.Application.Common.Interfaces;
using Odisea.Application.Suppliers.Connectors;
using Odisea.Application.Suppliers.Freshness;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Infrastructure.Data;
using Odisea.Infrastructure.Suppliers.Connectors;

namespace Odisea.UnitTests.Suppliers;

public class ConnectorSchedulerServiceTests
{
    private sealed class StubConnector(SupplierConnectionKind kind, ConnectorRunResult result) : IConnector
    {
        public SupplierConnectionKind Kind => kind;
        public Task<ConnectorRunResult> RunAsync(SupplierConnection c, CancellationToken ct) =>
            Task.FromResult(result);
    }

    // Builds a real DI container (scoped IAppDbContext + ImportRunner + FreshnessService)
    // over a shared in-memory store so the scheduler's per-tick scope sees seeded data.
    private static ServiceProvider BuildProvider(string dbName, IConnector connector)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IImportRunner, ImportRunner>();
        services.AddScoped<IFreshnessService, FreshnessService>();
        services.AddSingleton<IConnectorRegistry>(new ConnectorRegistry([connector]));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task OnStart_runsDueConnection_recordsJob_andAdvancesFreshness()
    {
        var dbName = Guid.NewGuid().ToString();
        var result = new ConnectorRunResult(3, 3, 0, [], DateTime.UtcNow);
        var provider = BuildProvider(dbName, new StubConnector(SupplierConnectionKind.Xml, result));

        // Seed an active, never-synced connection — due immediately.
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var op = new Operator { Name = "Op", Slug = "op" };
            db.Operators.Add(op);
            db.SupplierConnections.Add(new SupplierConnection
            {
                OperatorId = op.Id, Kind = SupplierConnectionKind.Xml, Name = "Nightly",
            });
            await db.SaveChangesAsync();
        }

        // Long poll interval so only the immediate first pass runs during the test.
        var options = new ConnectorSchedulerOptions
        {
            Enabled = true,
            PollInterval = TimeSpan.FromHours(1),
            SyncInterval = TimeSpan.FromHours(6),
        };
        var service = new ConnectorSchedulerService(provider, options, NullLogger<ConnectorSchedulerService>.Instance);

        await service.StartAsync(default);
        var job = await WaitForJob(provider, TimeSpan.FromSeconds(5));
        await service.StopAsync(default);

        Assert.NotNull(job);
        Assert.Equal(ImportJobStatus.Succeeded, job!.Status);

        await using var verify = provider.CreateAsyncScope();
        var vdb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var conn = await vdb.SupplierConnections.FirstAsync();
        Assert.NotNull(conn.LastSyncedAt); // freshness advanced by the successful run
    }

    [Fact]
    public async Task OnStart_skipsConnectionSyncedWithinInterval()
    {
        var dbName = Guid.NewGuid().ToString();
        var result = new ConnectorRunResult(3, 3, 0, [], DateTime.UtcNow);
        var provider = BuildProvider(dbName, new StubConnector(SupplierConnectionKind.Xml, result));

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var op = new Operator { Name = "Op", Slug = "op" };
            db.Operators.Add(op);
            db.SupplierConnections.Add(new SupplierConnection
            {
                OperatorId = op.Id, Kind = SupplierConnectionKind.Xml, Name = "Fresh",
                LastSyncedAt = DateTime.UtcNow.AddMinutes(-5),
            });
            await db.SaveChangesAsync();
        }

        var options = new ConnectorSchedulerOptions
        {
            Enabled = true,
            PollInterval = TimeSpan.FromHours(1),
            SyncInterval = TimeSpan.FromHours(6),
        };
        var service = new ConnectorSchedulerService(provider, options, NullLogger<ConnectorSchedulerService>.Instance);

        await service.StartAsync(default);
        var job = await WaitForJob(provider, TimeSpan.FromMilliseconds(500));
        await service.StopAsync(default);

        Assert.Null(job); // nothing was due, so no run happened
    }

    private static async Task<ImportJob?> WaitForJob(IServiceProvider provider, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var job = await db.ImportJobs.FirstOrDefaultAsync();
            if (job is not null) return job;
            await Task.Delay(25);
        }
        return null;
    }
}
