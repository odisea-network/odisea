using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.Features.Auth;
using Odisea.Modules.Agencies.Infrastructure;
using Odisea.SharedKernel;
using Xunit;

namespace Odisea.UnitTests.Agencies;

public class AuthServiceTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
    }

    private readonly FakeClock _clock = new();
    private readonly PasswordHasher<UserAccount> _hasher = new();
    private readonly AgenciesDbContext _db;
    private readonly AuthService _service;

    private const string Password = "Correct-Horse-Battery-42!";

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<AgenciesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AgenciesDbContext(options);

        var jwt = Options.Create(new JwtOptions
        {
            Secret = "unit-test-secret-at-least-32-bytes-long!",
        });
        _service = new AuthService(_db, _hasher, new TokenService(jwt, _clock), _clock);
    }

    private OperatorUser AddOperator(string email = "admin@test.local", bool isActive = true)
    {
        var user = new OperatorUser
        {
            Email = email,
            PasswordHash = "",
            FullName = "Test Admin",
            Role = OperatorRole.Admin,
            IsActive = isActive,
        };
        user.PasswordHash = _hasher.HashPassword(user, Password);
        _db.OperatorUsers.Add(user);
        _db.SaveChanges();
        return user;
    }

    [Fact]
    public async Task Valid_credentials_return_token_with_identity()
    {
        AddOperator();

        var result = await _service.LoginAsync("Admin@Test.Local", Password, CancellationToken.None);

        Assert.NotEmpty(result.AccessToken);
        Assert.Equal("operator", result.UserType);
        Assert.Equal("Admin", result.Role);
        Assert.Null(result.AgencyId);
        Assert.Equal(_clock.UtcNow.AddMinutes(60), result.ExpiresAt);
    }

    [Fact]
    public async Task Agency_user_token_carries_agency_id()
    {
        var agency = new Agency { Name = "Blue Horizon", Country = "BG" };
        _db.Agencies.Add(agency);
        var user = new AgencyUser
        {
            AgencyId = agency.Id,
            Email = "agent@test.local",
            PasswordHash = "",
            FullName = "Test Agent",
        };
        user.PasswordHash = _hasher.HashPassword(user, Password);
        _db.AgencyUsers.Add(user);
        _db.SaveChanges();

        var result = await _service.LoginAsync("agent@test.local", Password, CancellationToken.None);

        Assert.Equal("agency", result.UserType);
        Assert.Equal(agency.Id, result.AgencyId);
    }

    [Fact]
    public async Task Unknown_email_and_wrong_password_throw_the_same_exception()
    {
        AddOperator();

        var unknown = await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => _service.LoginAsync("nobody@test.local", Password, CancellationToken.None));
        var wrongPassword = await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => _service.LoginAsync("admin@test.local", "Wrong-Password-99!", CancellationToken.None));

        Assert.Equal(unknown.Message, wrongPassword.Message);
    }

    [Fact]
    public async Task Inactive_account_is_indistinguishable_from_wrong_credentials()
    {
        AddOperator(isActive: false);

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => _service.LoginAsync("admin@test.local", Password, CancellationToken.None));
    }

    [Fact]
    public async Task Fifth_failed_attempt_locks_the_account()
    {
        var user = AddOperator();

        for (var i = 0; i < UserAccount.MaxFailedLoginAttempts; i++)
        {
            await Assert.ThrowsAsync<InvalidCredentialsException>(
                () => _service.LoginAsync(user.Email, "Wrong-Password-99!", CancellationToken.None));
        }

        Assert.True(user.IsLockedOut(_clock.UtcNow));

        // Even the correct password is rejected while locked.
        await Assert.ThrowsAsync<AccountLockedException>(
            () => _service.LoginAsync(user.Email, Password, CancellationToken.None));
    }

    [Fact]
    public async Task Lockout_expires_after_the_window()
    {
        var user = AddOperator();
        for (var i = 0; i < UserAccount.MaxFailedLoginAttempts; i++)
        {
            await Assert.ThrowsAsync<InvalidCredentialsException>(
                () => _service.LoginAsync(user.Email, "Wrong-Password-99!", CancellationToken.None));
        }

        _clock.UtcNow += UserAccount.LockoutDuration + TimeSpan.FromSeconds(1);

        var result = await _service.LoginAsync(user.Email, Password, CancellationToken.None);
        Assert.NotEmpty(result.AccessToken);
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockedOutUntil);
    }

    [Fact]
    public async Task Successful_login_resets_the_failure_counter()
    {
        var user = AddOperator();

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => _service.LoginAsync(user.Email, "Wrong-Password-99!", CancellationToken.None));
        Assert.Equal(1, user.FailedLoginAttempts);

        await _service.LoginAsync(user.Email, Password, CancellationToken.None);
        Assert.Equal(0, user.FailedLoginAttempts);
    }
}
