using Microsoft.EntityFrameworkCore;
using Odisea.Application.Suppliers.Connectors;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Infrastructure.Data;
using Odisea.Infrastructure.Suppliers.Connectors;

namespace Odisea.UnitTests.Suppliers;

public class ImportRunnerTests
{
    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private sealed class StubConnector(SupplierConnectionKind kind, ConnectorRunResult result) : IConnector
    {
        public SupplierConnectionKind Kind => kind;
        public Task<ConnectorRunResult> RunAsync(SupplierConnection c, CancellationToken ct) =>
            Task.FromResult(result);
    }

    private sealed class ThrowingConnector(SupplierConnectionKind kind) : IConnector
    {
        public SupplierConnectionKind Kind => kind;
        public Task<ConnectorRunResult> RunAsync(SupplierConnection c, CancellationToken ct) =>
            throw new InvalidOperationException("boom");
    }

    private static async Task<SupplierConnection> SeedConnection(AppDbContext db, SupplierConnectionKind kind)
    {
        var op = new Operator { Name = "Op", Slug = "op" };
        db.Operators.Add(op);
        var conn = new SupplierConnection { OperatorId = op.Id, Kind = kind, Name = "Conn" };
        db.SupplierConnections.Add(conn);
        await db.SaveChangesAsync();
        return conn;
    }

    [Fact]
    public async Task RunAsync_SuccessfulRun_RecordsSucceededJobAndAdvancesFreshness()
    {
        await using var db = NewDb();
        var conn = await SeedConnection(db, SupplierConnectionKind.Xml);

        var result = new ConnectorRunResult(10, 9, 1, [], DateTime.UtcNow);
        var registry = new ConnectorRegistry([new StubConnector(SupplierConnectionKind.Xml, result)]);
        var runner = new ImportRunner(db, registry);

        var job = await runner.RunAsync(conn, default);

        Assert.Equal(ImportJobStatus.Succeeded, job.Status);
        Assert.Equal(10, job.OffersFetched);
        Assert.Equal(9, job.OffersImported);
        Assert.Equal(1, job.OffersDeactivated);
        Assert.Empty(job.Errors);
        Assert.NotNull(job.CompletedAt);

        var reloaded = await db.SupplierConnections.FirstAsync(c => c.Id == conn.Id);
        Assert.Equal(result.RanAt, reloaded.LastSyncedAt);

        Assert.Equal(1, await db.ImportJobs.CountAsync());
    }

    [Fact]
    public async Task RunAsync_FailedRun_RecordsFailedJobAndLeavesFreshnessUntouched()
    {
        await using var db = NewDb();
        var conn = await SeedConnection(db, SupplierConnectionKind.JsonApi);
        var priorSync = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        conn.LastSyncedAt = priorSync;
        await db.SaveChangesAsync();

        var result = new ConnectorRunResult(5, 0, 0, ["bad row 3", "bad row 7"], DateTime.UtcNow);
        var registry = new ConnectorRegistry([new StubConnector(SupplierConnectionKind.JsonApi, result)]);
        var runner = new ImportRunner(db, registry);

        var job = await runner.RunAsync(conn, default);

        Assert.Equal(ImportJobStatus.Failed, job.Status);
        Assert.Contains("bad row 3", job.Errors);
        Assert.Contains("bad row 7", job.Errors);

        var reloaded = await db.SupplierConnections.FirstAsync(c => c.Id == conn.Id);
        Assert.Equal(priorSync, reloaded.LastSyncedAt); // unchanged on failure
    }

    [Fact]
    public async Task RunAsync_ThrowingConnector_RecordsFailedJobInsteadOfBubbling()
    {
        await using var db = NewDb();
        var conn = await SeedConnection(db, SupplierConnectionKind.CsvSftp);

        var registry = new ConnectorRegistry([new ThrowingConnector(SupplierConnectionKind.CsvSftp)]);
        var runner = new ImportRunner(db, registry);

        var job = await runner.RunAsync(conn, default);

        Assert.Equal(ImportJobStatus.Failed, job.Status);
        Assert.Contains("boom", job.Errors);
        Assert.Equal(1, await db.ImportJobs.CountAsync());
    }
}
