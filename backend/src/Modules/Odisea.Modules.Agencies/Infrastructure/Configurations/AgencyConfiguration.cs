using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odisea.Modules.Agencies.Domain;

namespace Odisea.Modules.Agencies.Infrastructure.Configurations;

public class AgencyConfiguration : IEntityTypeConfiguration<Agency>
{
    public void Configure(EntityTypeBuilder<Agency> builder)
    {
        builder.Property(a => a.Name).HasMaxLength(200);
        builder.Property(a => a.Country).HasMaxLength(2);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.CreditLimit).HasPrecision(18, 2);
        builder.Property(a => a.ContactEmail).HasMaxLength(320);
        builder.Property(a => a.LegalName).HasMaxLength(200);
        builder.Property(a => a.Eik).HasMaxLength(13);
        builder.Property(a => a.VatNumber).HasMaxLength(15);
        builder.Property(a => a.BillingAddress).HasMaxLength(300);
        builder.Property(a => a.BillingCity).HasMaxLength(100);
        builder.Property(a => a.Mol).HasMaxLength(200);
        builder.HasIndex(a => a.Name);
    }
}

public class CommissionLevelConfiguration : IEntityTypeConfiguration<CommissionLevel>
{
    public void Configure(EntityTypeBuilder<CommissionLevel> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(100);
        builder.Property(c => c.Percent).HasPrecision(5, 2);
    }
}
