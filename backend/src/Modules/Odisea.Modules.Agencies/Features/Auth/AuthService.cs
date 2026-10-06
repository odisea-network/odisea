using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.Infrastructure;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.SharedKernel;

namespace Odisea.Modules.Agencies.Features.Auth;

public class AuthService(
    AgenciesDbContext db,
    IPasswordHasher<UserAccount> hasher,
    TokenService tokenService,
    IClock clock)
{
    // Target for the dummy verification below; never persisted.
    private static readonly OperatorUser Dummy =
        new() { Email = "", PasswordHash = "", FullName = "" };

    public async Task<LoginResponse> LoginAsync(string email, string password, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();

        UserAccount? user =
            await db.OperatorUsers.FirstOrDefaultAsync(u => u.Email == normalized, ct)
            ?? (UserAccount?)await db.AgencyUsers.FirstOrDefaultAsync(u => u.Email == normalized, ct);

        if (user is null || !user.IsActive)
        {
            // Burn a hash computation so response time doesn't reveal whether the email exists.
            hasher.HashPassword(Dummy, password);
            throw new InvalidCredentialsException();
        }

        var now = clock.UtcNow;
        if (user.IsLockedOut(now))
            throw new AccountLockedException(user.LockedOutUntil!.Value);

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            user.RegisterFailedLogin(now);
            await db.SaveChangesAsync(ct);
            throw new InvalidCredentialsException();
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = hasher.HashPassword(user, password);

        user.RegisterSuccessfulLogin();
        await db.SaveChangesAsync(ct);

        var (token, expiresAt) = tokenService.IssueFor(user);
        return new LoginResponse(
            token,
            expiresAt,
            user is OperatorUser ? AuthClaims.UserTypeOperator : AuthClaims.UserTypeAgency,
            user switch
            {
                OperatorUser op => op.Role.ToString(),
                AgencyUser agent => agent.Role.ToString(),
                _ => "",
            },
            (user as AgencyUser)?.AgencyId,
            user.FullName);
    }
}
