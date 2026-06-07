using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Odisea.Application.Common.Interfaces;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;

namespace Odisea.Infrastructure.Services;

public class JwtService(IConfiguration config) : IJwtService
{
    private readonly string _secret = config["Jwt:Secret"]
        ?? throw new InvalidOperationException("Jwt:Secret is not configured.");
    private readonly string _issuer = config["Jwt:Issuer"] ?? "odisea-api";
    private readonly string _audience = config["Jwt:Audience"] ?? "odisea-client";
    private readonly int _accessTokenMinutes =
        int.TryParse(config["Jwt:AccessTokenExpiryMinutes"], out var m) ? m : 15;

    public (string accessToken, DateTime expiresAt) GenerateAccessToken(User user, Membership? membership)
    {
        if (_secret.Length < 32)
            throw new InvalidOperationException("Jwt:Secret must be at least 32 characters.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(_accessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, membership?.Role.ToString() ?? UserRole.PlatformAdmin.ToString()),
        };

        if (membership is not null)
        {
            claims.Add(new Claim("tenantType", membership.TenantType.ToString()));
            if (membership.TenantId.HasValue)
                claims.Add(new Claim("tenantId", membership.TenantId.Value.ToString()));
        }

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public (string rawToken, string hashedToken) GenerateRefreshToken()
    {
        // 32 bytes → 256 bits of entropy, base64url for safe transport
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        var raw = Convert.ToBase64String(bytes);

        // Store only the SHA-256 hash; compromising the DB row doesn't yield a usable token.
        var hashed = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        return (raw, hashed);
    }
}
