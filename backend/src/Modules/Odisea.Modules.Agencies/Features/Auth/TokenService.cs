using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.SharedKernel;

namespace Odisea.Modules.Agencies.Features.Auth;

public class TokenService(IOptions<JwtOptions> options, IClock clock)
{
    public (string Token, DateTimeOffset ExpiresAt) IssueFor(UserAccount user)
    {
        var jwt = options.Value;
        var now = clock.UtcNow;
        var expires = now.AddMinutes(jwt.AccessTokenMinutes);

        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Name, user.FullName),
        ];

        switch (user)
        {
            case OperatorUser op:
                claims.Add(new Claim(AuthClaims.UserType, AuthClaims.UserTypeOperator));
                claims.Add(new Claim(AuthClaims.Role, op.Role.ToString()));
                break;
            case AgencyUser agent:
                claims.Add(new Claim(AuthClaims.UserType, AuthClaims.UserTypeAgency));
                claims.Add(new Claim(AuthClaims.Role, agent.Role.ToString()));
                claims.Add(new Claim(AuthClaims.AgencyId, agent.AgencyId.ToString()));
                break;
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret));
        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
