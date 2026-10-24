using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.Domain;

namespace Odisea.Modules.Agencies.Infrastructure;

public class AgenciesDbContext(DbContextOptions<AgenciesDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public const string Schema = "agencies";

    public DbSet<Agency> Agencies => Set<Agency>();
    public DbSet<AgencyUser> AgencyUsers => Set<AgencyUser>();
    public DbSet<OperatorUser> OperatorUsers => Set<OperatorUser>();
    public DbSet<CommissionLevel> CommissionLevels => Set<CommissionLevel>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AgencyApiKey> AgencyApiKeys => Set<AgencyApiKey>();

    /// The DataProtection key ring (agencies.data_protection_keys). Offer tokens and
    /// encrypted passenger documents are unreadable without these rows — never truncate.
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgenciesDbContext).Assembly);
    }
}
