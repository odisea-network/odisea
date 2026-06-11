using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odisea.Domain.Entities;

namespace Odisea.Infrastructure.Data.Configurations;

public class SupplierConnectionConfiguration : IEntityTypeConfiguration<SupplierConnection>
{
    public void Configure(EntityTypeBuilder<SupplierConnection> b)
    {
        b.ToTable("supplier_connections");
        b.HasKey(x => x.Id);

        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.ConfigJson).HasColumnType("jsonb").IsRequired();

        // Enums as strings for readability in the DB.
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);

        b.HasOne(x => x.Operator)
            .WithMany(o => o.Connections)
            .HasForeignKey(x => x.OperatorId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.OperatorId);
    }
}
