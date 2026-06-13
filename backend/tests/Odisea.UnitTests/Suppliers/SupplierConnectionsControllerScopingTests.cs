using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Suppliers.Connectors;
using Odisea.Application.Suppliers.Dtos;
using Odisea.Application.Suppliers.Freshness;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Infrastructure.Data;
using Odisea.Infrastructure.Suppliers.Connectors;
using Odisea.UnitTests.Helpers;
using Odisea.WebAPI.Controllers;

namespace Odisea.UnitTests.Suppliers;

// Guards tenant isolation: the supplier-connections endpoints must only ever
// surface the calling operator's own connections.
public class SupplierConnectionsControllerScopingTests
{
    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private static SupplierConnectionsController ControllerFor(AppDbContext db, Guid operatorId)
    {
        var registry = new ConnectorRegistry([new ManualConnector()]);
        return new SupplierConnectionsController(
            db,
            new ImportRunner(db, registry),
            new FreshnessService(db),
            new FakeOperatorContext(operatorId));
    }

    private static async Task<SupplierConnection> SeedConnection(AppDbContext db, Guid operatorId, string name)
    {
        var conn = new SupplierConnection
        {
            OperatorId = operatorId,
            Kind = SupplierConnectionKind.Manual,
            Name = name,
            Status = SupplierConnectionStatus.Active,
        };
        db.SupplierConnections.Add(conn);
        await db.SaveChangesAsync();
        return conn;
    }

    [Fact]
    public async Task List_returnsOnlyCallingOperatorsConnections()
    {
        await using var db = NewDb();
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();
        await SeedConnection(db, mine, "Mine");
        await SeedConnection(db, theirs, "Theirs");

        var result = await ControllerFor(db, mine).List(default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<SupplierConnectionDto>>(ok.Value);
        Assert.Single(dtos);
        Assert.Equal("Mine", dtos.First().Name);
    }

    [Fact]
    public async Task Health_returnsOnlyCallingOperatorsConnections()
    {
        await using var db = NewDb();
        var mine = Guid.NewGuid();
        await SeedConnection(db, mine, "Mine");
        await SeedConnection(db, Guid.NewGuid(), "Theirs");

        var result = await ControllerFor(db, mine).Health(default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var rows = Assert.IsAssignableFrom<IEnumerable<ConnectionHealthDto>>(ok.Value);
        Assert.Single(rows);
        Assert.Equal("Mine", rows.First().Name);
    }

    [Fact]
    public async Task Get_anotherOperatorsConnection_returns404()
    {
        await using var db = NewDb();
        var theirs = await SeedConnection(db, Guid.NewGuid(), "Theirs");

        var result = await ControllerFor(db, Guid.NewGuid()).Get(theirs.Id, default);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Run_anotherOperatorsConnection_returns404_andRecordsNoJob()
    {
        await using var db = NewDb();
        var theirs = await SeedConnection(db, Guid.NewGuid(), "Theirs");

        var result = await ControllerFor(db, Guid.NewGuid()).Run(theirs.Id, default);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(0, await db.ImportJobs.CountAsync());
    }

    [Fact]
    public async Task Sweep_anotherOperatorsConnection_returns404()
    {
        await using var db = NewDb();
        var theirs = await SeedConnection(db, Guid.NewGuid(), "Theirs");

        var result = await ControllerFor(db, Guid.NewGuid()).Sweep(theirs.Id, default);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Jobs_anotherOperatorsConnection_returns404()
    {
        await using var db = NewDb();
        var theirs = await SeedConnection(db, Guid.NewGuid(), "Theirs");

        var result = await ControllerFor(db, Guid.NewGuid()).Jobs(theirs.Id, default);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Run_ownConnection_succeeds()
    {
        await using var db = NewDb();
        var mine = Guid.NewGuid();
        var conn = await SeedConnection(db, mine, "Mine");

        var result = await ControllerFor(db, mine).Run(conn.Id, default);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, await db.ImportJobs.CountAsync());
    }

    [Fact]
    public async Task Create_setsOperatorOwnershipAndDefaults()
    {
        await using var db = NewDb();
        var mine = Guid.NewGuid();

        var req = new CreateSupplierConnectionRequest(
            Kind: "JsonApi", Name: "Nightly", ConfigJson: """{"url":"https://x"}""", FreshnessTtlHours: null);
        var result = await ControllerFor(db, mine).Create(req, default);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var dto = Assert.IsType<SupplierConnectionDto>(created.Value);
        Assert.Equal("Nightly", dto.Name);
        Assert.Equal("JsonApi", dto.Kind);
        Assert.Equal(24, dto.FreshnessTtlHours);

        var saved = await db.SupplierConnections.SingleAsync();
        Assert.Equal(mine, saved.OperatorId);
        Assert.Equal(SupplierConnectionStatus.Active, saved.Status);
    }

    [Fact]
    public async Task Create_invalidKind_returns400()
    {
        await using var db = NewDb();
        var result = await ControllerFor(db, Guid.NewGuid())
            .Create(new CreateSupplierConnectionRequest("Telepathy", "X", null, null), default);

        Assert.Equal(400, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task Create_invalidConfigJson_returns400()
    {
        await using var db = NewDb();
        var result = await ControllerFor(db, Guid.NewGuid())
            .Create(new CreateSupplierConnectionRequest("Manual", "X", "not json", null), default);

        Assert.Equal(400, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task Update_anotherOperatorsConnection_returns404()
    {
        await using var db = NewDb();
        var theirs = await SeedConnection(db, Guid.NewGuid(), "Theirs");

        var result = await ControllerFor(db, Guid.NewGuid())
            .Update(theirs.Id, new UpdateSupplierConnectionRequest("Renamed", null, null, null), default);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Update_ownConnection_appliesSuppliedFields()
    {
        await using var db = NewDb();
        var mine = Guid.NewGuid();
        var conn = await SeedConnection(db, mine, "Old");

        var result = await ControllerFor(db, mine).Update(
            conn.Id, new UpdateSupplierConnectionRequest("New", """{"url":"u"}""", "Paused", 48), default);

        Assert.IsType<OkObjectResult>(result);
        var saved = await db.SupplierConnections.SingleAsync();
        Assert.Equal("New", saved.Name);
        Assert.Equal(SupplierConnectionStatus.Paused, saved.Status);
        Assert.Equal(48, saved.FreshnessTtlHours);
    }

    [Fact]
    public async Task Delete_anotherOperatorsConnection_returns404_andKeepsIt()
    {
        await using var db = NewDb();
        var theirs = await SeedConnection(db, Guid.NewGuid(), "Theirs");

        var result = await ControllerFor(db, Guid.NewGuid()).Delete(theirs.Id, default);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(1, await db.SupplierConnections.CountAsync());
    }

    [Fact]
    public async Task Delete_ownConnection_returns204()
    {
        await using var db = NewDb();
        var mine = Guid.NewGuid();
        var conn = await SeedConnection(db, mine, "Mine");

        var result = await ControllerFor(db, mine).Delete(conn.Id, default);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(0, await db.SupplierConnections.CountAsync());
    }
}
