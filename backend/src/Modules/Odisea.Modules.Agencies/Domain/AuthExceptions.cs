using Odisea.SharedKernel;

namespace Odisea.Modules.Agencies.Domain;

// Deliberately identical message for unknown email, wrong password, and
// deactivated accounts — the response must not reveal which one it was.
public sealed class InvalidCredentialsException()
    : DomainException("Invalid email or password.");

public sealed class AccountLockedException(DateTimeOffset lockedUntil)
    : DomainException("Account is temporarily locked due to repeated failed logins.")
{
    public DateTimeOffset LockedUntil { get; } = lockedUntil;
}

public sealed class DuplicateEmailException(string email)
    : DomainException($"A user with email '{email}' already exists.");
