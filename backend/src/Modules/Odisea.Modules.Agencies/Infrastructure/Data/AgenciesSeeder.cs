using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Odisea.Modules.Agencies.Domain;

namespace Odisea.Modules.Agencies.Infrastructure.Data;

public static class AgenciesSeeder
{
    public static async Task SeedAsync(
        AgenciesDbContext db,
        IPasswordHasher<UserAccount> hasher,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken ct = default)
    {
        if (await db.OperatorUsers.AnyAsync(ct))
            return;

        var email = configuration["Seed:Operator:Email"];
        var password = configuration["Seed:Operator:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "No operator users exist and Seed:Operator:{{Email,Password}} are not configured — nobody can log in");
            return;
        }

        if (password.Length < 12)
            throw new InvalidOperationException("Seed:Operator:Password must be at least 12 characters.");

        var admin = new OperatorUser
        {
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = "",
            FullName = configuration["Seed:Operator:FullName"] ?? "Operator Admin",
            Role = OperatorRole.Admin,
        };
        admin.PasswordHash = hasher.HashPassword(admin, password);

        db.OperatorUsers.Add(admin);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded operator admin {Email}", admin.Email);
    }
}
