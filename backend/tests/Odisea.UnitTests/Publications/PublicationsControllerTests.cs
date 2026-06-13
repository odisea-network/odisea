using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Publications.Dtos;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Infrastructure.Data;
using Odisea.UnitTests.Helpers;
using Odisea.WebAPI.Controllers;

namespace Odisea.UnitTests.Publications;

public class PublicationsControllerTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static PublicationsController CreateController(
        AppDbContext db,
        string? origin = null,
        Guid? agencyId = null)
    {
        var controller = new PublicationsController(db, new FakeAgencyContext(agencyId), new FakeWebhookDispatcher());
        var httpContext = new DefaultHttpContext();
        if (origin is not null)
            httpContext.Request.Headers.Origin = origin;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static async Task<(AppDbContext db, Publication pub)> SeedPublishedPublication(
        string key,
        string[] allowedDomains)
    {
        var db = CreateDb();

        var collection = new Collection
        {
            AgencyId = Guid.NewGuid(),
            Name = "Test Collection",
            Slug = "test-col",
            Status = CollectionStatus.Published,
        };
        db.Collections.Add(collection);

        var pub = new Publication
        {
            Key = key,
            AgencyId = collection.AgencyId,
            CollectionId = collection.Id,
            Status = PublicationStatus.Published,
            Version = 1,
            AllowedDomains = [.. allowedDomains.Select(d => new AllowedDomain { Domain = d })],
        };
        db.Publications.Add(pub);
        await db.SaveChangesAsync();

        return (db, pub);
    }

    // ── Public GET manifest ────────────────────────────────────────────────────

    [Fact]
    public async Task GetManifest_KnownKey_Returns200WithManifest()
    {
        var (db, _) = await SeedPublishedPublication("testkey001", []);
        await using var _ = db;

        var controller = CreateController(db);
        var result = await controller.GetManifest("testkey001", default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var manifest = Assert.IsType<PublicationManifestDto>(ok.Value);
        Assert.Equal("testkey001", manifest.Key);
        Assert.Equal(1, manifest.Version);
        Assert.Equal("Published", manifest.Status);
        // Offers are keyed by global collection id, not the per-agency slug (#18).
        Assert.Equal($"/api/v1/collections/{manifest.CollectionId}/offers", manifest.OffersUrl);
        Assert.Equal("\"1-", manifest.ETag[..3]);
    }

    [Fact]
    public async Task GetManifest_UnknownKey_Returns404()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);

        var result = await controller.GetManifest("doesnotexist", default);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetManifest_GuidShapedKey_Returns404()
    {
        // The public endpoint refuses GUID-shaped inputs (those route to {id:guid}).
        await using var db = CreateDb();
        var controller = CreateController(db);

        var result = await controller.GetManifest(Guid.NewGuid().ToString(), default);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetManifest_ServesRegardlessOfOrigin_OriginEnforcedInMiddleware()
    {
        // The controller no longer checks Origin — EmbedSecurityMiddleware owns that
        // (see EmbedSecurityMiddlewareTests). The action serves any reachable request.
        var (db, _) = await SeedPublishedPublication("openkey001", ["agency-blue.bg"]);
        await using var _ = db;

        var controller = CreateController(db, origin: "https://some-random-site.bg");
        var result = await controller.GetManifest("openkey001", default);

        Assert.IsType<OkObjectResult>(result);
    }

    // ── Management ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidRequest_Returns201WithKey()
    {
        await using var db = CreateDb();
        var agencyId = Guid.NewGuid();

        var collection = new Collection
        {
            AgencyId = agencyId,
            Name = "Col",
            Slug = "col",
            Status = CollectionStatus.Published,
        };
        db.Collections.Add(collection);
        await db.SaveChangesAsync();

        // AgencyId comes from the JWT context, not the request body.
        var controller = CreateController(db, agencyId: agencyId);
        var req = new CreatePublicationRequest(
            AgencyId: collection.AgencyId,
            CollectionId: collection.Id,
            ThemeId: null,
            ExperienceId: null,
            ExperienceConfig: null,
            AllowedDomains: null);

        var result = await controller.Create(req, default);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var dto = Assert.IsType<PublicationDto>(created.Value);
        Assert.False(string.IsNullOrEmpty(dto.Key));
        Assert.Equal("Draft", dto.Status);
        Assert.Equal(0, dto.Version);
    }

    [Fact]
    public async Task Publish_FlipsStatusAndIncrementsVersion()
    {
        await using var db = CreateDb();

        var collection = new Collection
        {
            AgencyId = Guid.NewGuid(),
            Name = "Col",
            Slug = "col-pub",
            Status = CollectionStatus.Published,
        };
        db.Collections.Add(collection);

        var pub = new Publication
        {
            Key = "publishme01",
            AgencyId = collection.AgencyId,
            CollectionId = collection.Id,
            Status = PublicationStatus.Draft,
            Version = 0,
        };
        db.Publications.Add(pub);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.Publish(pub.Id, default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<PublicationDto>(ok.Value);
        Assert.Equal("Published", dto.Status);
        Assert.Equal(1, dto.Version);
    }

    [Fact]
    public async Task Publish_DispatchesPublicationPublishedWebhook()
    {
        await using var db = CreateDb();
        var collection = new Collection { AgencyId = Guid.NewGuid(), Name = "Col", Slug = "col-wh", Status = CollectionStatus.Published };
        db.Collections.Add(collection);
        var pub = new Publication
        {
            Key = "publishme02", AgencyId = collection.AgencyId, CollectionId = collection.Id,
            Status = PublicationStatus.Draft, Version = 0,
        };
        db.Publications.Add(pub);
        await db.SaveChangesAsync();

        var webhooks = new FakeWebhookDispatcher();
        var controller = new PublicationsController(db, new FakeAgencyContext(collection.AgencyId), webhooks)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        await controller.Publish(pub.Id, default);

        var dispatched = Assert.Single(webhooks.Dispatched);
        Assert.Equal("publication.published", dispatched.EventType);
        Assert.Equal(collection.AgencyId, dispatched.AgencyId);
    }

    [Fact]
    public async Task Publish_AlreadyPublished_DoesNotRedispatch()
    {
        var (db, pub) = await SeedPublishedPublication("alreadypub1", []);
        await using var _ = db;

        var webhooks = new FakeWebhookDispatcher();
        var controller = new PublicationsController(db, new FakeAgencyContext(pub.AgencyId), webhooks)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        await controller.Publish(pub.Id, default);

        Assert.Empty(webhooks.Dispatched);
    }
}
