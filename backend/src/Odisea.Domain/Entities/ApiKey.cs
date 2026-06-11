using System.Security.Cryptography;
using System.Text;
using Odisea.Domain.Common;

namespace Odisea.Domain.Entities;

/// <summary>
/// A bearer credential an embed/API consumer presents as
/// <c>Authorization: ApiKey od_xxxxxxxx_...</c>. The raw key is shown exactly
/// once at creation; only its SHA-256 hash and a display prefix are persisted.
/// </summary>
public class ApiKey : Entity
{
    public Guid AgencyId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>SHA-256 (base64) of the raw key. The raw value is never stored.</summary>
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>First 8 chars of the raw key — safe to show in management UI.</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>Comma-separated scope list, e.g. "publications:read,events:write".</summary>
    public string Scopes { get; set; } = string.Empty;

    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public bool IsActive(DateTime now) =>
        RevokedAt is null && (ExpiresAt is null || ExpiresAt > now);

    public IEnumerable<string> ScopeList =>
        Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private const string KeyPrefix = "od_";

    /// <summary>
    /// Builds a new key for the given agency/scopes. Returns the raw key (return
    /// it to the caller once, then discard) alongside the persistable entity whose
    /// <see cref="KeyHash"/> and <see cref="Prefix"/> are already populated.
    /// </summary>
    public static (string rawKey, ApiKey entity) Generate(
        Guid agencyId, string name, IEnumerable<string> scopes, DateTime? expiresAt = null)
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        var rawKey = KeyPrefix + Base64Url(bytes);

        var entity = new ApiKey
        {
            AgencyId = agencyId,
            Name = name,
            KeyHash = Hash(rawKey),
            Prefix = rawKey[..8],
            Scopes = string.Join(',', scopes),
            ExpiresAt = expiresAt,
        };

        return (rawKey, entity);
    }

    /// <summary>SHA-256 hash used for both storage and constant-cost lookup.</summary>
    public static string Hash(string rawKey) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey)));

    private static string Base64Url(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
