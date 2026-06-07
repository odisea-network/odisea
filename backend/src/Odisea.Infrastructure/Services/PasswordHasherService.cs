using Microsoft.AspNetCore.Identity;
using Odisea.Application.Common.Interfaces;
using Odisea.Domain.Entities;

namespace Odisea.Infrastructure.Services;

public class PasswordHasherService : IPasswordHasherService
{
    private readonly PasswordHasher<User> _hasher = new();

    // Pre-computed hash used when the user is not found; ensures the verification
    // path always costs one PBKDF2 round regardless of whether the email exists,
    // preventing timing-based email enumeration.
    private static readonly string _dummyHash =
        new PasswordHasher<User>().HashPassword(new User(), "dummy_timing_guard_!");

    public string Hash(string password) =>
        _hasher.HashPassword(new User(), password);

    public bool Verify(string? storedHash, string password)
    {
        var hash = storedHash ?? _dummyHash;
        var result = _hasher.VerifyHashedPassword(new User(), hash, password);
        // storedHash == null → always false even if dummy matched (impossible but explicit)
        return storedHash is not null && result != PasswordVerificationResult.Failed;
    }
}
