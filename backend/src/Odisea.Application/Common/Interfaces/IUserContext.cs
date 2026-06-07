using Odisea.Domain.Enums;

namespace Odisea.Application.Common.Interfaces;

public interface IUserContext
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    string Email { get; }
    UserRole Role { get; }
}
