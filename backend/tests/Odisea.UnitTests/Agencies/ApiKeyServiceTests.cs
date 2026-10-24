using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.Features.ApiKeys;
using Odisea.Modules.Agencies.Infrastructure;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.SharedKernel;
using Xunit;

namespace Odisea.UnitTests.Agencies;

public class ApiKeyServiceTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
    }

    private readonly AgenciesDbContext _db;
    private readonly FakeClock _clock = new();
    private readonly ApiKeyService _service;
    private readonly Agency _agency = new() { Name = "Blue Horizon", Country = "BG" };
    private readonly Guid _operatorId = Guid.NewGuid();

    public ApiKeyServiceTests()
    {
        _db = new AgenciesDbContext(new DbContextOptionsBuilder<AgenciesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _db.Agencies.Add(_agency);
        _db.SaveChanges();
        _service = new ApiKeyService(_db, _clock);
    }

    private async Task<ApiKeyCreatedDto> CreateAsync(DateTimeOffset? expiresAt = null)
    {
        var created = await _service.CreateAsync(
            _agency.Id, new CreateApiKeyRequest("Partner integration", expiresAt), _operatorId, CancellationToken.None);
        Assert.NotNull(created);
        return created;
    }

    [Fact]
    public async Task Created_keys_carry_the_marker_and_only_their_hash_is_stored()
    {
        var created = await CreateAsync();

        Assert.StartsWith(ApiKeyAuthenticationDefaults.KeyPrefixMarker, created.Secret);
        Assert.Equal(ApiKeyService.KeyLength, created.Secret.Length);
        Assert.Equal(created.Secret[..16], created.Key.KeyPrefix);

        var stored = await _db.AgencyApiKeys.SingleAsync();
        Assert.Equal(ApiKeyService.Hash(created.Secret), stored.KeyHash);
        Assert.DoesNotContain(created.Secret, stored.KeyHash);
        Assert.Equal(_operatorId, stored.CreatedByUserId);
    }

    [Fact]
    public async Task Two_keys_never_collide()
    {
        var first = await CreateAsync();
        var second = await CreateAsync();
        Assert.NotEqual(first.Secret, second.Secret);
        Assert.NotEqual(first.Key.KeyPrefix, second.Key.KeyPrefix);
    }

    [Fact]
    public async Task An_active_key_authenticates_as_its_agency_and_records_first_use()
    {
        var created = await CreateAsync();

        var principal = await _service.AuthenticateAsync(created.Secret, CancellationToken.None);

        Assert.NotNull(principal);
        Assert.Equal(_agency.Id, principal.AgencyId);
        Assert.Equal(created.Key.Id, principal.KeyId);
        Assert.Equal(_clock.UtcNow, (await _db.AgencyApiKeys.SingleAsync()).LastUsedAt);
    }

    [Fact]
    public async Task Last_use_is_written_at_most_every_five_minutes()
    {
        var created = await CreateAsync();
        var first = _clock.UtcNow;
        await _service.AuthenticateAsync(created.Secret, CancellationToken.None);

        _clock.UtcNow = first.AddMinutes(2);
        await _service.AuthenticateAsync(created.Secret, CancellationToken.None);
        Assert.Equal(first, (await _db.AgencyApiKeys.SingleAsync()).LastUsedAt);

        _clock.UtcNow = first.AddMinutes(6);
        await _service.AuthenticateAsync(created.Secret, CancellationToken.None);
        Assert.Equal(first.AddMinutes(6), (await _db.AgencyApiKeys.SingleAsync()).LastUsedAt);
    }

    [Fact]
    public async Task Unknown_malformed_revoked_and_expired_keys_do_not_authenticate()
    {
        var revoked = await CreateAsync();
        Assert.True(await _service.RevokeAsync(_agency.Id, revoked.Key.Id, CancellationToken.None));

        var expiring = await CreateAsync(expiresAt: _clock.UtcNow.AddHours(1));
        _clock.UtcNow = _clock.UtcNow.AddHours(2);

        Assert.Null(await _service.AuthenticateAsync("odk_live_" + new string('a', 43), CancellationToken.None));
        Assert.Null(await _service.AuthenticateAsync("not-a-key", CancellationToken.None));
        Assert.Null(await _service.AuthenticateAsync(revoked.Secret, CancellationToken.None));
        Assert.Null(await _service.AuthenticateAsync(expiring.Secret, CancellationToken.None));
    }

    [Fact]
    public async Task Keys_of_suspended_agencies_stop_working_immediately()
    {
        var created = await CreateAsync();
        _agency.Status = AgencyStatus.Suspended;
        await _db.SaveChangesAsync();

        Assert.Null(await _service.AuthenticateAsync(created.Secret, CancellationToken.None));
    }

    [Fact]
    public async Task Revoke_is_scoped_to_the_agency_and_idempotent()
    {
        var created = await CreateAsync();

        Assert.False(await _service.RevokeAsync(Guid.NewGuid(), created.Key.Id, CancellationToken.None));
        Assert.True(await _service.RevokeAsync(_agency.Id, created.Key.Id, CancellationToken.None));
        var revokedAt = (await _db.AgencyApiKeys.SingleAsync()).RevokedAt;

        _clock.UtcNow = _clock.UtcNow.AddDays(1);
        Assert.True(await _service.RevokeAsync(_agency.Id, created.Key.Id, CancellationToken.None));
        Assert.Equal(revokedAt, (await _db.AgencyApiKeys.SingleAsync()).RevokedAt);
    }

    [Fact]
    public async Task Expiry_must_be_in_the_future_and_the_agency_must_exist()
    {
        await Assert.ThrowsAsync<ApiKeyValidationException>(() => CreateAsync(expiresAt: _clock.UtcNow));
        Assert.Null(await _service.CreateAsync(
            Guid.NewGuid(), new CreateApiKeyRequest("ghost"), _operatorId, CancellationToken.None));
        Assert.Null(await _service.ListAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Listing_shows_prefixes_but_never_secrets()
    {
        var created = await CreateAsync();

        var list = await _service.ListAsync(_agency.Id, CancellationToken.None);

        Assert.NotNull(list);
        var only = Assert.Single(list);
        Assert.Equal(created.Key.KeyPrefix, only.KeyPrefix);
        Assert.DoesNotContain(only.GetType().GetProperties(), p => p.Name.Contains("Secret") || p.Name.Contains("Hash"));
    }
}
