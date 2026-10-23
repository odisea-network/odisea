using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.Infrastructure;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.SharedKernel;

namespace Odisea.Modules.Agencies.Features.Auth;

public class RefreshTokenService(AgenciesDbContext db, IOptions<JwtOptions> options, IClock clock)
{
    public async Task<string> IssueAsync(UserAccount user, CancellationToken ct)
    {
        var (raw, entity) = Create(user, Guid.NewGuid());
        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync(ct);
        return raw;
    }

    /// Rotation: the presented token is single-use. Presenting one that was
    /// already used or revoked means it was stolen (or replayed) — the whole
    /// family dies so neither party keeps a working session.
    public async Task<(UserAccount User, string NewRefreshToken)> RotateAsync(string rawToken, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == Hash(rawToken), ct)
            ?? throw new InvalidRefreshTokenException();

        if (!token.IsActive(now))
        {
            await RevokeFamilyAsync(token.FamilyId, now, ct);
            throw new InvalidRefreshTokenException();
        }

        var user = await LoadUserAsync(token, ct);
        if (user is null || !user.IsActive)
        {
            await RevokeFamilyAsync(token.FamilyId, now, ct);
            throw new InvalidRefreshTokenException();
        }

        token.UsedAt = now;
        var (raw, next) = Create(user, token.FamilyId);
        db.RefreshTokens.Add(next);
        await db.SaveChangesAsync(ct);
        return (user, raw);
    }

    public async Task RevokeByRawTokenAsync(string rawToken, CancellationToken ct)
    {
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == Hash(rawToken), ct);
        if (token is not null)
        {
            await RevokeFamilyAsync(token.FamilyId, clock.UtcNow, ct);
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken ct)
    {
        var family = await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var member in family)
            member.RevokedAt = now;
        await db.SaveChangesAsync(ct);
    }

    private (string Raw, RefreshToken Entity) Create(UserAccount user, Guid familyId)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var entity = new RefreshToken
        {
            UserId = user.Id,
            UserType = user is OperatorUser ? AuthClaims.UserTypeOperator : AuthClaims.UserTypeAgency,
            TokenHash = Hash(raw),
            FamilyId = familyId,
            ExpiresAt = clock.UtcNow.AddDays(options.Value.RefreshTokenDays),
        };
        return (raw, entity);
    }

    private async Task<UserAccount?> LoadUserAsync(RefreshToken token, CancellationToken ct) =>
        token.UserType == AuthClaims.UserTypeOperator
            ? await db.OperatorUsers.FirstOrDefaultAsync(u => u.Id == token.UserId, ct)
            : await db.AgencyUsers.FirstOrDefaultAsync(u => u.Id == token.UserId, ct);

    private static string Hash(string raw) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
