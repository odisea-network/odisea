using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.ApiKeys.Dtos;
using Odisea.Domain.Common;
using Odisea.Domain.Entities;
using Odisea.Infrastructure.Data;
using Odisea.UnitTests.Helpers;
using Odisea.WebAPI.Controllers;

namespace Odisea.UnitTests.EmbedSecurity;

public class ApiKeysControllerTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ApiKeysController CreateController(AppDbContext db, Guid? agencyId = null) =>
        new(db, new FakeAgencyContext(agencyId));

    [Fact]
    public async Task Create_ValidRequest_Returns201AndRawKeyOnce()
    {
        await using var db = CreateDb();
        var agencyId = Guid.NewGuid();
        var controller = CreateController(db, agencyId);

        var req = new CreateApiKeyRequest("Analytics key", [ApiKeyScopes.EventsWrite], null);
        var result = await controller.Create(req, default);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var payload = Assert.IsType<CreateApiKeyResult>(created.Value);

        Assert.StartsWith("od_", payload.RawKey);
        Assert.Equal(payload.RawKey[..8], payload.Key.Prefix);
        Assert.Contains(ApiKeyScopes.EventsWrite, payload.Key.Scopes);

        // The stored row holds only the hash, never the raw key.
        var stored = await db.ApiKeys.SingleAsync();
        Assert.Equal(ApiKey.Hash(payload.RawKey), stored.KeyHash);
        Assert.DoesNotContain(payload.RawKey, stored.KeyHash);
    }

    [Fact]
    public async Task Create_UnknownScope_Returns400()
    {
        await using var db = CreateDb();
        var controller = CreateController(db, Guid.NewGuid());

        var result = await controller.Create(
            new CreateApiKeyRequest("Bad", ["offers:delete"], null), default);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, obj.StatusCode);
    }

    [Fact]
    public async Task Create_NoScopes_Returns400()
    {
        await using var db = CreateDb();
        var controller = CreateController(db, Guid.NewGuid());

        var result = await controller.Create(new CreateApiKeyRequest("Empty", [], null), default);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, obj.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsOnlyCallersAgencyKeys()
    {
        await using var db = CreateDb();
        var mine = Guid.NewGuid();
        var other = Guid.NewGuid();

        db.ApiKeys.Add(ApiKey.Generate(mine, "a", [ApiKeyScopes.PublicationsRead]).entity);
        db.ApiKeys.Add(ApiKey.Generate(other, "b", [ApiKeyScopes.PublicationsRead]).entity);
        await db.SaveChangesAsync();

        var controller = CreateController(db, mine);
        var result = await controller.List(default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var keys = Assert.IsAssignableFrom<IEnumerable<ApiKeyDto>>(ok.Value);
        Assert.All(keys, k => Assert.Equal(mine, k.AgencyId));
        Assert.Single(keys);
    }

    [Fact]
    public async Task Revoke_SetsRevokedAt_Returns204()
    {
        await using var db = CreateDb();
        var agencyId = Guid.NewGuid();
        var (_, key) = ApiKey.Generate(agencyId, "k", [ApiKeyScopes.EventsWrite]);
        db.ApiKeys.Add(key);
        await db.SaveChangesAsync();

        var controller = CreateController(db, agencyId);
        var result = await controller.Revoke(key.Id, default);

        Assert.IsType<NoContentResult>(result);
        var stored = await db.ApiKeys.SingleAsync();
        Assert.NotNull(stored.RevokedAt);
    }

    [Fact]
    public async Task Revoke_ForeignAgency_Returns403()
    {
        await using var db = CreateDb();
        var (_, key) = ApiKey.Generate(Guid.NewGuid(), "k", [ApiKeyScopes.EventsWrite]);
        db.ApiKeys.Add(key);
        await db.SaveChangesAsync();

        var controller = CreateController(db, Guid.NewGuid());
        var result = await controller.Revoke(key.Id, default);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, obj.StatusCode);
    }
}
