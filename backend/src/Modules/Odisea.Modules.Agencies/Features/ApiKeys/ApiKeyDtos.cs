using System.ComponentModel.DataAnnotations;
using Odisea.Modules.Agencies.Domain;

namespace Odisea.Modules.Agencies.Features.ApiKeys;

public record CreateApiKeyRequest(
    [Required, MaxLength(100)] string Name,
    DateTimeOffset? ExpiresAt = null);

/// Never carries the secret. The prefix is what operators see in lists.
public record ApiKeyDto(
    Guid Id,
    Guid AgencyId,
    string Name,
    string KeyPrefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt,
    DateTimeOffset? LastUsedAt);

/// Returned exactly once, from the create call. The secret is not recoverable afterwards.
public record ApiKeyCreatedDto(ApiKeyDto Key, string Secret);

public static class ApiKeyDtoMapping
{
    public static ApiKeyDto ToDto(this AgencyApiKey k) => new(
        k.Id, k.AgencyId, k.Name, k.KeyPrefix, k.CreatedAt, k.ExpiresAt, k.RevokedAt, k.LastUsedAt);
}
