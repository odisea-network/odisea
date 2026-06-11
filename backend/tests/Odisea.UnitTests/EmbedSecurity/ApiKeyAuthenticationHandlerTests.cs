using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Odisea.Domain.Common;
using Odisea.Domain.Entities;
using Odisea.Infrastructure.Data;
using Odisea.WebAPI.Auth;

namespace Odisea.UnitTests.EmbedSecurity;

public class ApiKeyAuthenticationHandlerTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // Minimal IOptionsMonitor that returns a fresh options instance for any scheme.
    private sealed class StubMonitor : IOptionsMonitor<AuthenticationSchemeOptions>
    {
        public AuthenticationSchemeOptions CurrentValue { get; } = new();
        public AuthenticationSchemeOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<AuthenticationSchemeOptions, string?> listener) => null;
    }

    private static async Task<AuthenticateResult> AuthenticateAsync(
        AppDbContext db, string? authorizationHeader)
    {
        var handler = new ApiKeyAuthenticationHandler(
            new StubMonitor(), NullLoggerFactory.Instance, UrlEncoder.Default, db);

        var context = new DefaultHttpContext();
        if (authorizationHeader is not null)
            context.Request.Headers.Authorization = authorizationHeader;

        var scheme = new AuthenticationScheme(
            ApiKeyAuthenticationHandler.SchemeName, null, typeof(ApiKeyAuthenticationHandler));
        await handler.InitializeAsync(scheme, context);

        return await handler.AuthenticateAsync();
    }

    [Fact]
    public async Task ValidKey_Succeeds_WithAgencyAndScopeClaims()
    {
        await using var db = CreateDb();
        var agencyId = Guid.NewGuid();
        var (rawKey, entity) = ApiKey.Generate(
            agencyId, "k", [ApiKeyScopes.EventsWrite, ApiKeyScopes.PublicationsRead]);
        db.ApiKeys.Add(entity);
        await db.SaveChangesAsync();

        var result = await AuthenticateAsync(db, $"ApiKey {rawKey}");

        Assert.True(result.Succeeded);
        var principal = result.Principal!;
        Assert.Equal(agencyId.ToString(), principal.FindFirstValue("tenantId"));
        Assert.Contains(principal.Claims,
            c => c.Type == ApiKeyAuthenticationHandler.ScopeClaimType && c.Value == ApiKeyScopes.EventsWrite);
    }

    [Fact]
    public async Task RevokedKey_Fails()
    {
        await using var db = CreateDb();
        var (rawKey, entity) = ApiKey.Generate(Guid.NewGuid(), "k", [ApiKeyScopes.EventsWrite]);
        entity.RevokedAt = DateTime.UtcNow.AddMinutes(-1);
        db.ApiKeys.Add(entity);
        await db.SaveChangesAsync();

        var result = await AuthenticateAsync(db, $"ApiKey {rawKey}");

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
    }

    [Fact]
    public async Task ExpiredKey_Fails()
    {
        await using var db = CreateDb();
        var (rawKey, entity) = ApiKey.Generate(
            Guid.NewGuid(), "k", [ApiKeyScopes.EventsWrite], DateTime.UtcNow.AddDays(-1));
        db.ApiKeys.Add(entity);
        await db.SaveChangesAsync();

        var result = await AuthenticateAsync(db, $"ApiKey {rawKey}");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task UnknownKey_Fails()
    {
        await using var db = CreateDb();
        var result = await AuthenticateAsync(db, "ApiKey od_does-not-exist");
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task BearerScheme_ReturnsNoResult_SoJwtCanHandleIt()
    {
        // Precedence: a JWT bearer request must be left for the JWT handler, not
        // failed by the ApiKey handler. NoResult lets the next scheme try.
        await using var db = CreateDb();
        var result = await AuthenticateAsync(db, "Bearer some.jwt.token");

        Assert.False(result.Succeeded);
        Assert.True(result.None);
    }

    [Fact]
    public async Task NoAuthorizationHeader_ReturnsNoResult()
    {
        await using var db = CreateDb();
        var result = await AuthenticateAsync(db, null);
        Assert.True(result.None);
    }
}
