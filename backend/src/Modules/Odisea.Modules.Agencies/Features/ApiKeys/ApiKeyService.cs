using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.Infrastructure;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.SharedKernel;

namespace Odisea.Modules.Agencies.Features.ApiKeys;

/// Who a presented key authenticates as. Carries only ids and a display name.
public sealed record ApiKeyPrincipal(Guid KeyId, Guid AgencyId, string Name);

public class ApiKeyService(AgenciesDbContext db, IClock clock)
{
    private const int SecretBytes = 32;
    private const int PrefixLength = 16;

    /// odk_live_ + base64url(32 bytes) = 9 + 43 characters.
    public const int KeyLength = 52;

    /// A busy partner would otherwise turn every request into a write.
    private static readonly TimeSpan LastUsedWriteInterval = TimeSpan.FromMinutes(5);

    /// Null when the agency does not exist (the controller turns that into a 404).
    public async Task<ApiKeyCreatedDto?> CreateAsync(
        Guid agencyId, CreateApiKeyRequest request, Guid createdByUserId, CancellationToken ct)
    {
        if (!await db.Agencies.AnyAsync(a => a.Id == agencyId, ct))
            return null;

        var now = clock.UtcNow;
        if (request.ExpiresAt is { } expiresAt && expiresAt <= now)
            throw new ApiKeyValidationException("The expiry must be in the future.");

        var secret = GenerateSecret();
        var key = new AgencyApiKey
        {
            AgencyId = agencyId,
            Name = request.Name.Trim(),
            KeyPrefix = secret[..PrefixLength],
            KeyHash = Hash(secret),
            CreatedByUserId = createdByUserId,
            ExpiresAt = request.ExpiresAt,
            CreatedAt = now,
        };
        db.AgencyApiKeys.Add(key);
        await db.SaveChangesAsync(ct);
        return new ApiKeyCreatedDto(key.ToDto(), secret);
    }

    public async Task<List<ApiKeyDto>?> ListAsync(Guid agencyId, CancellationToken ct)
    {
        if (!await db.Agencies.AnyAsync(a => a.Id == agencyId, ct))
            return null;

        return await db.AgencyApiKeys.AsNoTracking()
            .Where(k => k.AgencyId == agencyId)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => k.ToDto())
            .ToListAsync(ct);
    }

    /// Idempotent; false when the key does not belong to that agency.
    public async Task<bool> RevokeAsync(Guid agencyId, Guid keyId, CancellationToken ct)
    {
        var key = await db.AgencyApiKeys.FirstOrDefaultAsync(k => k.Id == keyId && k.AgencyId == agencyId, ct);
        if (key is null)
            return false;

        key.RevokedAt ??= clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// Resolves a presented key to the agency it authenticates. Null for unknown, revoked and
    /// expired keys and for suspended agencies alike — callers must not say which.
    public async Task<ApiKeyPrincipal?> AuthenticateAsync(string presentedKey, CancellationToken ct)
    {
        if (presentedKey.Length != KeyLength
            || !presentedKey.StartsWith(ApiKeyAuthenticationDefaults.KeyPrefixMarker, StringComparison.Ordinal))
            return null;

        var hash = Hash(presentedKey);
        var match = await db.AgencyApiKeys
            .Where(k => k.KeyHash == hash)
            .Join(db.Agencies, k => k.AgencyId, a => a.Id, (k, a) => new { Key = k, AgencyStatus = a.Status })
            .FirstOrDefaultAsync(ct);

        var now = clock.UtcNow;
        if (match is null || !match.Key.IsActive(now) || match.AgencyStatus != AgencyStatus.Active)
            return null;

        if (match.Key.LastUsedAt is null || now - match.Key.LastUsedAt > LastUsedWriteInterval)
        {
            match.Key.LastUsedAt = now;
            await db.SaveChangesAsync(ct);
        }

        return new ApiKeyPrincipal(match.Key.Id, match.Key.AgencyId, match.Key.Name);
    }

    public static string Hash(string raw) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    private static string GenerateSecret() =>
        ApiKeyAuthenticationDefaults.KeyPrefixMarker
        + Convert.ToBase64String(RandomNumberGenerator.GetBytes(SecretBytes))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
