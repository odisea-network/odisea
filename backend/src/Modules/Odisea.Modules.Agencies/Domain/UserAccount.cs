using Odisea.SharedKernel;

namespace Odisea.Modules.Agencies.Domain;

public abstract class UserAccount : Entity
{
    public const int MaxFailedLoginAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string FullName { get; set; }
    public bool IsActive { get; set; } = true;
    public int FailedLoginAttempts { get; private set; }
    public DateTimeOffset? LockedOutUntil { get; private set; }

    public bool IsLockedOut(DateTimeOffset now) => LockedOutUntil is { } until && until > now;

    public void RegisterFailedLogin(DateTimeOffset now)
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= MaxFailedLoginAttempts)
        {
            LockedOutUntil = now + LockoutDuration;
            FailedLoginAttempts = 0;
        }
    }

    public void RegisterSuccessfulLogin()
    {
        FailedLoginAttempts = 0;
        LockedOutUntil = null;
    }
}
