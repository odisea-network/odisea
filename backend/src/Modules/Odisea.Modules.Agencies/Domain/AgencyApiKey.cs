using Odisea.SharedKernel;

namespace Odisea.Modules.Agencies.Domain;

/// A machine credential for an external agency. Only the SHA-256 hash of the key is
/// stored; the plaintext is shown once at creation. The prefix exists so operators can
/// tell keys apart in a list without ever seeing the secret again.
public class AgencyApiKey : Entity
{
    public required Guid AgencyId { get; set; }
    public required string Name { get; set; }
    public required string KeyPrefix { get; set; }
    public required string KeyHash { get; set; }
    public required Guid CreatedByUserId { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }

    public bool IsActive(DateTimeOffset now) =>
        RevokedAt is null && (ExpiresAt is null || ExpiresAt > now);
}

public sealed class ApiKeyValidationException(string message) : DomainException(message);
