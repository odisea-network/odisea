namespace Odisea.SharedKernel;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    Guid? AgencyId { get; }
    string? Role { get; }
}
