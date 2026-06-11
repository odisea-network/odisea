using Odisea.Domain.Entities;

namespace Odisea.Application.ApiKeys.Dtos;

/// <summary>
/// Safe representation of an API key — exposes the prefix for identification but
/// never the secret. Returned by list/get; the raw key is only ever in
/// <see cref="CreateApiKeyResult"/>.
/// </summary>
public record ApiKeyDto(
    Guid Id,
    Guid AgencyId,
    string Name,
    string Prefix,
    string[] Scopes,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    DateTime? RevokedAt
);

public record CreateApiKeyRequest(
    string Name,
    string[] Scopes,
    DateTime? ExpiresAt
);

/// <summary>
/// The only place the raw key is ever returned. Show it to the user once at
/// creation — it cannot be retrieved again.
/// </summary>
public record CreateApiKeyResult(
    ApiKeyDto Key,
    string RawKey
);

public static class ApiKeyMappings
{
    public static ApiKeyDto ToDto(this ApiKey k) => new(
        k.Id, k.AgencyId, k.Name, k.Prefix, [.. k.ScopeList],
        k.CreatedAt, k.ExpiresAt, k.RevokedAt);
}
