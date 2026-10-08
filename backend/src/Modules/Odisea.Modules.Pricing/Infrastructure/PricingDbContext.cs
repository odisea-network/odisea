using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Pricing.Domain;

namespace Odisea.Modules.Pricing.Infrastructure;

public class PricingDbContext(DbContextOptions<PricingDbContext> options) : DbContext(options)
{
    public const string Schema = "pricing";

    public DbSet<PricingRule> PricingRules => Set<PricingRule>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        var rule = modelBuilder.Entity<PricingRule>();
        rule.Property(r => r.Name).HasMaxLength(200);
        rule.Property(r => r.Scope).HasConversion<string>().HasMaxLength(20);
        rule.Property(r => r.Kind).HasConversion<string>().HasMaxLength(20);
        rule.Property(r => r.ValueType).HasConversion<string>().HasMaxLength(20);
        rule.Property(r => r.Value).HasPrecision(12, 4);
        rule.HasIndex(r => new { r.Scope, r.ScopeId });

        var rate = modelBuilder.Entity<ExchangeRate>();
        rate.Property(r => r.FromCurrency).HasMaxLength(3);
        rate.Property(r => r.ToCurrency).HasMaxLength(3);
        rate.Property(r => r.Rate).HasPrecision(18, 8);
        rate.HasIndex(r => new { r.FromCurrency, r.ToCurrency, r.AsOf }).IsUnique();
    }
}
