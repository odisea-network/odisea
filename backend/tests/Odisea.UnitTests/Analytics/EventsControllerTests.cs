using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Analytics.Dtos;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Infrastructure.Data;
using Odisea.WebAPI.Controllers;

namespace Odisea.UnitTests.Analytics;

public class EventsControllerTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static EventsController CreateController(AppDbContext db)
    {
        var controller = new EventsController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        return controller;
    }

    private static async Task SeedPublication(AppDbContext db, string key)
    {
        db.Publications.Add(new Publication
        {
            Key = key,
            AgencyId = Guid.NewGuid(),
            CollectionId = Guid.NewGuid(),
            Status = PublicationStatus.Published,
            Version = 1,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Ingest_Batch_PersistsAllEventsForKnownKey()
    {
        await using var db = CreateDb();
        await SeedPublication(db, "knownkey001");
        var controller = CreateController(db);

        var cmd = new IngestEventCommand(new List<EventDto>
        {
            new("Impression", "knownkey001", null, "WebComponent", null),
            new("Open", "knownkey001", Guid.NewGuid(), "WebComponent", null),
            new("InquirySubmit", "knownkey001", null, null, null),
        });

        var result = await controller.Ingest(cmd, default);

        Assert.IsType<AcceptedResult>(result);
        Assert.Equal(3, await db.Events.CountAsync());
    }

    [Fact]
    public async Task Ingest_UnknownKey_IsDroppedSilentlyAnd202()
    {
        await using var db = CreateDb();
        await SeedPublication(db, "knownkey002");
        var controller = CreateController(db);

        var cmd = new IngestEventCommand(new List<EventDto>
        {
            new("Impression", "knownkey002", null, null, null),
            new("Impression", "ghost-key-xx", null, null, null),
            new("Open", "ghost-key-xx", null, null, null),
        });

        var result = await controller.Ingest(cmd, default);

        Assert.IsType<AcceptedResult>(result);
        // only the known-key event survived
        Assert.Equal(1, await db.Events.CountAsync());
        Assert.True(await db.Events.AllAsync(e => e.PublicationKey == "knownkey002"));
    }

    [Fact]
    public async Task Ingest_EmptyBatch_Returns202WithoutPersisting()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);

        var result = await controller.Ingest(new IngestEventCommand([]), default);

        Assert.IsType<AcceptedResult>(result);
        Assert.Equal(0, await db.Events.CountAsync());
    }

    [Fact]
    public async Task Ingest_HashesIpAndUserAgent_NeverStoresRaw()
    {
        await using var db = CreateDb();
        await SeedPublication(db, "knownkey003");
        var controller = CreateController(db);
        controller.HttpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.7");
        controller.HttpContext.Request.Headers.UserAgent = "Mozilla/5.0 test";

        await controller.Ingest(
            new IngestEventCommand([new("Impression", "knownkey003", null, null, null)]), default);

        var stored = await db.Events.SingleAsync();
        Assert.NotNull(stored.IpHash);
        Assert.NotNull(stored.UserAgentHash);
        Assert.DoesNotContain("203.0.113.7", stored.IpHash);
        Assert.DoesNotContain("Mozilla", stored.UserAgentHash);
        Assert.Equal(64, stored.IpHash!.Length); // SHA-256 hex
    }
}
