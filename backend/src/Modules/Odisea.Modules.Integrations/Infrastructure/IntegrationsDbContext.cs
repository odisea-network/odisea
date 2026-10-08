using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Integrations.Domain;

namespace Odisea.Modules.Integrations.Infrastructure;

public class IntegrationsDbContext(DbContextOptions<IntegrationsDbContext> options) : DbContext(options)
{
    public const string Schema = "integrations";

    public DbSet<ProviderCallLog> ProviderCallLogs => Set<ProviderCallLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        var log = modelBuilder.Entity<ProviderCallLog>();
        log.Property(l => l.ProviderCode).HasMaxLength(30);
        log.Property(l => l.Operation).HasMaxLength(30);
        log.Property(l => l.Detail).HasMaxLength(500);
        log.HasIndex(l => new { l.ProviderCode, l.CreatedAt });
    }
}
