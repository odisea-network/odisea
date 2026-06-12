using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odisea.Domain.Entities;

namespace Odisea.Infrastructure.Data.Configurations;

public class OperatorAgencyEntitlementConfiguration : IEntityTypeConfiguration<OperatorAgencyEntitlement>
{
    public void Configure(EntityTypeBuilder<OperatorAgencyEntitlement> b)
    {
        b.ToTable("operator_agency_entitlements");
        b.HasKey(x => x.Id);

        b.HasOne(x => x.Operator)
            .WithMany()
            .HasForeignKey(x => x.OperatorId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Agency)
            .WithMany()
            .HasForeignKey(x => x.AgencyId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.CommissionPercent).HasColumnType("numeric(5,2)");

        // One entitlement per (operator, agency) pair.
        b.HasIndex(x => new { x.OperatorId, x.AgencyId }).IsUnique();
    }
}
