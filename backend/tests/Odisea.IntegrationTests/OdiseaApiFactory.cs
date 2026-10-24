using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.Features.Auth;
using Odisea.Modules.Agencies.Infrastructure;

namespace Odisea.IntegrationTests;

public enum TestIdentity
{
    /// Operator with role Admin (seeded by AgenciesSeeder from Seed:Operator:*).
    Admin,
    /// Operator with role Operator (inserted by the factory; no endpoint creates operators).
    Operator,
    /// Agency user of the demo agency "Blue Horizon Travel" (seeded by DevDemoSeeder).
    Agent,
}

/// Boots the real host — Program.cs runs every module's migrations and both seeders —
/// against a dedicated Postgres database that is dropped and recreated once per run.
public sealed class OdiseaApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@odisea.local";
    public const string AdminPassword = "TestOnly-Odisea-Admin-2026!";
    public const string OperatorEmail = "ops@odisea.local";
    public const string OperatorPassword = "TestOnly-Odisea-Ops-2026!";
    public const string AgentEmail = "agent@odisea.local";
    public const string AgentPassword = "DevOnly-Odisea-Agent-2026!";

    public static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("ODISEA_TEST_CONNECTION")
        ?? "Host=localhost;Port=5433;Database=odisea_test;Username=odisea;Password=odisea";

    private readonly Dictionary<TestIdentity, string> _tokens = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = ConnectionString,
                ["Jwt:Secret"] = "integration-tests-only-jwt-signing-key-at-least-32-bytes-long",
                ["Jwt:AccessTokenMinutes"] = "60",
                ["Seed:DemoData"] = "true",
                ["Seed:Operator:Email"] = AdminEmail,
                ["Seed:Operator:Password"] = AdminPassword,
                ["Seed:Operator:FullName"] = "Test Operator Admin",
                ["Serilog:MinimumLevel:Default"] = "Warning",
                // Tests hammer one key; the 60/min production default is covered by manual smoke.
                ["RateLimiting:ApiKeyPerMinute"] = "100000",
            };

            // The test runner swallows the host's console output. Set ODISEA_TEST_LOG to a file
            // path to get the host's Serilog output (including request exceptions) when debugging.
            if (Environment.GetEnvironmentVariable("ODISEA_TEST_LOG") is { Length: > 0 } logPath)
            {
                settings["Serilog:WriteTo:1:Name"] = "File";
                settings["Serilog:WriteTo:1:Args:path"] = logPath;
            }

            config.AddInMemoryCollection(settings);
        });
    }

    public async Task InitializeAsync()
    {
        await DropDatabaseAsync();

        // Touching Server runs Program.cs: MigrateAsync for all six contexts, then the seeders.
        // If Postgres is unreachable, Program.cs logs Fatal and returns, and this times out.
        _ = Server;

        await SeedOperatorUserAsync();

        // One login per identity for the whole run: the "auth" rate limiter partitions on the
        // remote IP, which TestServer leaves null, so every login shares a single 10/min bucket.
        _tokens[TestIdentity.Admin] = await LoginAsync(AdminEmail, AdminPassword);
        _tokens[TestIdentity.Operator] = await LoginAsync(OperatorEmail, OperatorPassword);
        _tokens[TestIdentity.Agent] = await LoginAsync(AgentEmail, AgentPassword);
    }

    async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync();

    public HttpClient ClientAs(TestIdentity identity)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _tokens[identity]);
        return client;
    }

    public HttpClient AnonymousClient() => CreateClient();

    /// An external agency: no login, just the key header on every request.
    public HttpClient ClientWithApiKey(string apiKey)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return client;
    }

    private async Task<string> LoginAsync(string email, string password)
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        await response.ShouldBeAsync(HttpStatusCode.OK);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return login!.AccessToken;
    }

    private static async Task DropDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<AgenciesDbContext>().UseNpgsql(ConnectionString).Options;
        await using var db = new AgenciesDbContext(options);
        await db.Database.EnsureDeletedAsync();
    }

    private async Task SeedOperatorUserAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgenciesDbContext>();
        if (await db.OperatorUsers.AnyAsync(u => u.Email == OperatorEmail))
            return;

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<UserAccount>>();
        var user = new OperatorUser
        {
            Email = OperatorEmail,
            PasswordHash = "",
            FullName = "Test Operator",
            Role = OperatorRole.Operator,
        };
        user.PasswordHash = hasher.HashPassword(user, OperatorPassword);
        db.OperatorUsers.Add(user);
        await db.SaveChangesAsync();
    }
}
