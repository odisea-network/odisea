using Odisea.Domain.Entities;

namespace Odisea.Application.Common.Interfaces;

public interface IJwtService
{
    (string accessToken, DateTime expiresAt) GenerateAccessToken(User user, Membership? membership);

    // rawToken is sent to the client; hashedToken is stored in the DB.
    (string rawToken, string hashedToken) GenerateRefreshToken();
}
