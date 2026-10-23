using Odisea.SharedKernel;

namespace Odisea.Modules.Agencies.Domain;

/// One rotation link in a refresh-token family. Only the SHA-256 hash is
/// stored — a database leak exposes no usable tokens. A family is born at
/// login and dies at logout, expiry, or the moment a used token reappears
/// (classic token-theft signal).
public class RefreshToken : Entity
{
    public required Guid UserId { get; set; }
    public required string UserType { get; set; } // "operator" | "agency"
    public required string TokenHash { get; set; }
    public required Guid FamilyId { get; set; }
    public required DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public bool IsActive(DateTimeOffset now) =>
        UsedAt is null && RevokedAt is null && ExpiresAt > now;
}

public sealed class InvalidRefreshTokenException()
    : DomainException("The session is no longer valid. Please sign in again.");
