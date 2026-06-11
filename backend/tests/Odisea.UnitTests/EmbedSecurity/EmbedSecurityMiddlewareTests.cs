using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Infrastructure.Data;
using Odisea.WebAPI.Middleware;

namespace Odisea.UnitTests.EmbedSecurity;

public class EmbedSecurityMiddlewareTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<(int status, bool nextCalled)> RunAsync(
        AppDbContext db, string path, string? origin = null, string? referer = null, string method = "GET")
    {
        var nextCalled = false;
        var middleware = new EmbedSecurityMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            NullLogger<EmbedSecurityMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        if (origin is not null) context.Request.Headers.Origin = origin;
        if (referer is not null) context.Request.Headers.Referer = referer;

        await middleware.InvokeAsync(context, db);
        return (context.Response.StatusCode, nextCalled);
    }

    private static async Task<AppDbContext> SeedManifest(string key, params string[] domains)
    {
        var db = CreateDb();
        var pub = new Publication
        {
            Key = key,
            AgencyId = Guid.NewGuid(),
            CollectionId = Guid.NewGuid(),
            Status = PublicationStatus.Published,
            AllowedDomains = [.. domains.Select(d => new AllowedDomain { Domain = d })],
        };
        db.Publications.Add(pub);
        await db.SaveChangesAsync();
        return db;
    }

    // ── Manifest endpoint ──────────────────────────────────────────────────────

    [Fact]
    public async Task EmptyAllowlist_PassesThrough_WithWarning()
    {
        await using var db = await SeedManifest("openkey001");
        var (_, nextCalled) = await RunAsync(db, "/api/v1/publications/openkey001", "https://anything.com");
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task OriginInAllowlist_PassesThrough()
    {
        await using var db = await SeedManifest("restricted1", "agency-blue.bg", "agency-blue.com");
        var (_, nextCalled) = await RunAsync(db, "/api/v1/publications/restricted1", "https://agency-blue.bg");
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task OriginNotInAllowlist_Returns403()
    {
        await using var db = await SeedManifest("restricted2", "agency-blue.bg");
        var (status, nextCalled) = await RunAsync(db, "/api/v1/publications/restricted2", "https://evil.com");
        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task WildcardSubdomain_Matches()
    {
        await using var db = await SeedManifest("wild1", "*.example.com");
        var (_, sub) = await RunAsync(db, "/api/v1/publications/wild1", "https://shop.example.com");
        Assert.True(sub);

        await using var db2 = await SeedManifest("wild2", "*.example.com");
        var (_, apex) = await RunAsync(db2, "/api/v1/publications/wild2", "https://example.com");
        Assert.True(apex);

        await using var db3 = await SeedManifest("wild3", "*.example.com");
        var (status, blocked) = await RunAsync(db3, "/api/v1/publications/wild3", "https://example.org");
        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.False(blocked);
    }

    [Fact]
    public async Task RefererUsedWhenOriginAbsent()
    {
        await using var db = await SeedManifest("refkey1", "agency-blue.bg");
        var (_, nextCalled) = await RunAsync(
            db, "/api/v1/publications/refkey1", origin: null, referer: "https://agency-blue.bg/page");
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task NoOriginOrReferer_PassesThrough()
    {
        // Server-side fetch (no browser origin) is not governed by the allowlist.
        await using var db = await SeedManifest("srv1", "agency-blue.bg");
        var (_, nextCalled) = await RunAsync(db, "/api/v1/publications/srv1");
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task UnknownPublication_PassesThrough_To404()
    {
        await using var db = CreateDb();
        var (_, nextCalled) = await RunAsync(db, "/api/v1/publications/missing", "https://evil.com");
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task NonGuardedPath_PassesThrough()
    {
        await using var db = CreateDb();
        var (_, nextCalled) = await RunAsync(db, "/api/v1/api-keys", "https://evil.com");
        Assert.True(nextCalled);
    }

    // ── Collection offers endpoint ───────────────────────────────────────────────

    [Fact]
    public async Task CollectionOffers_UnionsPublicationDomains()
    {
        var db = CreateDb();
        var collection = new Collection
        {
            AgencyId = Guid.NewGuid(),
            Name = "C",
            Slug = "summer",
            Status = CollectionStatus.Published,
        };
        db.Collections.Add(collection);
        db.Publications.Add(new Publication
        {
            Key = "p1",
            AgencyId = collection.AgencyId,
            CollectionId = collection.Id,
            Status = PublicationStatus.Published,
            AllowedDomains = [new AllowedDomain { Domain = "agency-blue.bg" }],
        });
        await db.SaveChangesAsync();
        await using var _ = db;

        var (_, allowed) = await RunAsync(db, "/api/v1/collections/summer/offers", "https://agency-blue.bg");
        Assert.True(allowed);

        var (status, blocked) = await RunAsync(db, "/api/v1/collections/summer/offers", "https://evil.com");
        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.False(blocked);
    }
}
