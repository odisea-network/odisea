namespace Odisea.SharedKernel;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    Guid? AgencyId { get; }
    string? Role { get; }

    /// True when the caller authenticated with an agency API key rather than a user login.
    bool IsApiClient { get; }
}
