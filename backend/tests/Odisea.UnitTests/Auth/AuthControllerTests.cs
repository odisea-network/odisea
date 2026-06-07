using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Odisea.Application.Auth.Dtos;
using Odisea.Application.Common.Interfaces;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Infrastructure.Data;
using Odisea.Infrastructure.Services;
using Odisea.UnitTests.Helpers;
using Odisea.WebAPI.Controllers;

namespace Odisea.UnitTests.Auth;

public class AuthControllerTests
{
    private static AppDbContext BuildDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static IConfiguration BuildConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"]  = "test-secret-key-that-is-32-chars-long!",
                ["Jwt:Issuer"]  = "odisea-api",
                ["Jwt:Audience"] = "odisea-client",
                ["Jwt:AccessTokenExpiryMinutes"] = "15",
            })
            .Build();

    private static (AuthController ctrl, AppDbContext db, IJwtService jwt, IPasswordHasherService hasher)
        Build()
    {
        var db = BuildDb();
        var config = BuildConfig();
        var jwt = new JwtService(config);
        var hasher = new PasswordHasherService();
        var ctrl = new AuthController(db, jwt, hasher);
        return (ctrl, db, jwt, hasher);
    }

    // ── Register ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_NewUser_Returns200WithTokens()
    {
        var (ctrl, db, _, _) = Build();
        await using var _ = db;

        var agency = new Agency { Name = "Test Agency", Slug = "test" };
        db.Agencies.Add(agency);
        await db.SaveChangesAsync();

        var result = await ctrl.Register(
            new RegisterRequest("user@test.com", "Password1!", "Test User", agency.Id, null),
            default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var tokens = Assert.IsType<TokenResponse>(ok.Value);
        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
        Assert.True(tokens.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var (ctrl, db, _, hasher) = Build();
        await using var _ = db;

        var user = new User
        {
            Email = "dup@test.com",
            PasswordHash = hasher.Hash("Password1!"),
            DisplayName = "Dup",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await ctrl.Register(
            new RegisterRequest("dup@test.com", "Password1!", "Other", null, null),
            default);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, obj.StatusCode);
    }

    [Fact]
    public async Task Register_ShortPassword_Returns400()
    {
        var (ctrl, db, _, _) = Build();
        await using var _ = db;

        var result = await ctrl.Register(
            new RegisterRequest("new@test.com", "short", "Name", null, null),
            default);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, obj.StatusCode);
    }

    [Fact]
    public async Task Register_Email_IsNormalisedToLowercase()
    {
        var (ctrl, db, _, _) = Build();
        await using var _ = db;

        await ctrl.Register(
            new RegisterRequest("UPPER@Test.COM", "Password1!", "Name", null, null),
            default);

        var stored = await db.Users.FirstOrDefaultAsync(u => u.Email == "upper@test.com");
        Assert.NotNull(stored);
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_CorrectCredentials_Returns200WithTokens()
    {
        var (ctrl, db, _, hasher) = Build();
        await using var _ = db;

        db.Users.Add(new User
        {
            Email = "login@test.com",
            PasswordHash = hasher.Hash("Password1!"),
            DisplayName = "Login",
            Status = UserStatus.Active,
        });
        await db.SaveChangesAsync();

        var result = await ctrl.Login(new LoginRequest("login@test.com", "Password1!"), default);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<TokenResponse>(ok.Value);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var (ctrl, db, _, hasher) = Build();
        await using var _ = db;

        db.Users.Add(new User
        {
            Email = "pw@test.com",
            PasswordHash = hasher.Hash("RealPass1!"),
            DisplayName = "PW",
            Status = UserStatus.Active,
        });
        await db.SaveChangesAsync();

        var result = await ctrl.Login(new LoginRequest("pw@test.com", "WrongPass!"), default);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(401, obj.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var (ctrl, db, _, _) = Build();
        await using var _ = db;

        var result = await ctrl.Login(new LoginRequest("nobody@test.com", "Password1!"), default);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(401, obj.StatusCode);
    }

    [Fact]
    public async Task Login_SuspendedUser_Returns403()
    {
        var (ctrl, db, _, hasher) = Build();
        await using var _ = db;

        db.Users.Add(new User
        {
            Email = "susp@test.com",
            PasswordHash = hasher.Hash("Password1!"),
            DisplayName = "Suspended",
            Status = UserStatus.Suspended,
        });
        await db.SaveChangesAsync();

        var result = await ctrl.Login(new LoginRequest("susp@test.com", "Password1!"), default);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, obj.StatusCode);
    }

    // ── JWT round-trip ────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_AccessToken_ContainsExpectedClaims()
    {
        var (ctrl, db, jwt, hasher) = Build();
        await using var _ = db;

        var agency = new Agency { Name = "Acme", Slug = "acme" };
        db.Agencies.Add(agency);

        var user = new User
        {
            Email = "claims@test.com",
            PasswordHash = hasher.Hash("Password1!"),
            DisplayName = "Claims",
            Status = UserStatus.Active,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.Memberships.Add(new Membership
        {
            UserId = user.Id,
            TenantType = TenantType.Agency,
            TenantId = agency.Id,
            Role = UserRole.AgencyAdmin,
        });
        await db.SaveChangesAsync();

        var result = await ctrl.Login(new LoginRequest("claims@test.com", "Password1!"), default);
        var ok = Assert.IsType<OkObjectResult>(result);
        var tokens = Assert.IsType<TokenResponse>(ok.Value);

        // Decode and inspect claims without validating signature (read-only check)
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var parsed = handler.ReadJwtToken(tokens.AccessToken);

        Assert.Equal(user.Id.ToString(), parsed.Subject);
        Assert.Contains(parsed.Claims, c => c.Type == System.Security.Claims.ClaimTypes.Role
                                         && c.Value == "AgencyAdmin");
        Assert.Contains(parsed.Claims, c => c.Type == "tenantType" && c.Value == "Agency");
        Assert.Contains(parsed.Claims, c => c.Type == "tenantId" && c.Value == agency.Id.ToString());
    }

    // ── Refresh token ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_ValidToken_Issues_NewTokenPair()
    {
        var (ctrl, db, _, hasher) = Build();
        await using var _ = db;

        var loginResult = await ctrl.Register(
            new RegisterRequest("refresh@test.com", "Password1!", "Refresh", null, null),
            default);
        var firstTokens = Assert.IsType<TokenResponse>(((OkObjectResult)loginResult).Value);

        var result = await ctrl.Refresh(new RefreshRequest(firstTokens.RefreshToken), default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var newTokens = Assert.IsType<TokenResponse>(ok.Value);
        Assert.NotEqual(firstTokens.AccessToken, newTokens.AccessToken);
        Assert.NotEqual(firstTokens.RefreshToken, newTokens.RefreshToken);
    }

    [Fact]
    public async Task Refresh_TokenRotation_OldTokenIsRejected()
    {
        var (ctrl, db, _, _) = Build();
        await using var _ = db;

        var reg = Assert.IsType<OkObjectResult>(
            await ctrl.Register(
                new RegisterRequest("rotate@test.com", "Password1!", "Rotate", null, null),
                default));
        var first = Assert.IsType<TokenResponse>(reg.Value);

        // Use the refresh token once — it gets rotated.
        await ctrl.Refresh(new RefreshRequest(first.RefreshToken), default);

        // Reusing the old token must return 401.
        var result = await ctrl.Refresh(new RefreshRequest(first.RefreshToken), default);
        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(401, obj.StatusCode);
    }

    [Fact]
    public async Task Refresh_InvalidToken_Returns401()
    {
        var (ctrl, db, _, _) = Build();
        await using var _ = db;

        var result = await ctrl.Refresh(new RefreshRequest("bogus-token"), default);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(401, obj.StatusCode);
    }
}
