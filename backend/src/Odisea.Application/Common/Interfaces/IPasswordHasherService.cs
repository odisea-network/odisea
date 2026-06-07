namespace Odisea.Application.Common.Interfaces;

public interface IPasswordHasherService
{
    string Hash(string password);

    // storedHash == null → always returns false but still spends full hash time,
    // preventing timing-based email enumeration.
    bool Verify(string? storedHash, string password);
}
