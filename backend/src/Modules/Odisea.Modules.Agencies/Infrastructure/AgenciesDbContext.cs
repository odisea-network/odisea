using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.Domain;

namespace Odisea.Modules.Agencies.Infrastructure;

public class AgenciesDbContext(DbContextOptions<AgenciesDbContext> options) : DbContext(options)
{
    public const string Schema = "agencies";

    public DbSet<Agency> Agencies => Set<Agency>();
    public DbSet<AgencyUser> AgencyUsers => Set<AgencyUser>();
    public DbSet<OperatorUser> OperatorUsers => Set<OperatorUser>();
    public DbSet<CommissionLevel> CommissionLevels => Set<CommissionLevel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgenciesDbContext).Assembly);
    }
}
