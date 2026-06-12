using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odisea.Domain.Entities;

namespace Odisea.Infrastructure.Data.Configurations;

public class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
    public void Configure(EntityTypeBuilder<ImportJob> b)
    {
        b.ToTable("import_jobs");
        b.HasKey(x => x.Id);

        b.HasOne(x => x.SupplierConnection)
            .WithMany()
            .HasForeignKey(x => x.SupplierConnectionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Errors).HasColumnType("text");

        // Dashboard queries: "latest jobs for this connection" and "error trend".
        b.HasIndex(x => new { x.SupplierConnectionId, x.StartedAt });
    }
}
