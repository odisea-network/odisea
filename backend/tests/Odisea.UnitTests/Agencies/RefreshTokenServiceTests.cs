using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.Features.Auth;
using Odisea.Modules.Agencies.Infrastructure;
using Odisea.SharedKernel;
using Xunit;

namespace Odisea.UnitTests.Agencies;

public class RefreshTokenServiceTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
    }

    private readonly AgenciesDbContext _db;
    private readonly FakeClock _clock = new();
    private readonly RefreshTokenService _service;
    private readonly OperatorUser _user;

    public RefreshTokenServiceTests()
    {
        _db = new AgenciesDbContext(new DbContextOptionsBuilder<AgenciesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _service = new RefreshTokenService(
            _db,
            Options.Create(new JwtOptions { Secret = "unit-test-secret-at-least-32-bytes-long!", RefreshTokenDays = 14 }),
            _clock);
        _user = new OperatorUser { Email = "a@b.c", PasswordHash = "x", FullName = "A" };
        _db.OperatorUsers.Add(_user);
        _db.SaveChanges();
    }

    [Fact]
    public async Task Rotation_returns_a_new_token_and_kills_the_old_one()
    {
        var first = await _service.IssueAsync(_user, CancellationToken.None);

        var (user, second) = await _service.RotateAsync(first, CancellationToken.None);
        Assert.Equal(_user.Id, user.Id);
        Assert.NotEqual(first, second);

        // The spent token is single-use.
        await Assert.ThrowsAsync<InvalidRefreshTokenException>(
            () => _service.RotateAsync(first, CancellationToken.None));
    }

    [Fact]
    public async Task Reusing_a_spent_token_revokes_the_whole_family()
    {
        var first = await _service.IssueAsync(_user, CancellationToken.None);
        var (_, second) = await _service.RotateAsync(first, CancellationToken.None);

        // Replay of the used token = theft signal.
        await Assert.ThrowsAsync<InvalidRefreshTokenException>(
            () => _service.RotateAsync(first, CancellationToken.None));

        // The legitimate successor dies with the family.
        await Assert.ThrowsAsync<InvalidRefreshTokenException>(
            () => _service.RotateAsync(second, CancellationToken.None));
        Assert.All(_db.RefreshTokens, t => Assert.False(t.IsActive(_clock.UtcNow)));
    }

    [Fact]
    public async Task Expired_tokens_are_rejected()
    {
        var token = await _service.IssueAsync(_user, CancellationToken.None);
        _clock.UtcNow = _clock.UtcNow.AddDays(15);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(
            () => _service.RotateAsync(token, CancellationToken.None));
    }

    [Fact]
    public async Task Deactivated_user_cannot_refresh_and_loses_the_family()
    {
        var token = await _service.IssueAsync(_user, CancellationToken.None);
        _user.IsActive = false;
        _db.SaveChanges();

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(
            () => _service.RotateAsync(token, CancellationToken.None));
        Assert.All(_db.RefreshTokens, t => Assert.NotNull(t.RevokedAt));
    }

    [Fact]
    public async Task Logout_revokes_the_family()
    {
        var token = await _service.IssueAsync(_user, CancellationToken.None);
        await _service.RevokeByRawTokenAsync(token, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(
            () => _service.RotateAsync(token, CancellationToken.None));
    }

    [Fact]
    public async Task Unknown_token_is_rejected_without_side_effects()
    {
        await _service.IssueAsync(_user, CancellationToken.None);
        await Assert.ThrowsAsync<InvalidRefreshTokenException>(
            () => _service.RotateAsync("forged-token", CancellationToken.None));
        Assert.Single(_db.RefreshTokens, t => t.IsActive(_clock.UtcNow));
    }
}
