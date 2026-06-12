using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odisea.Domain.Entities;

namespace Odisea.Infrastructure.Data.Configurations;

public class SourceOfferConfiguration : IEntityTypeConfiguration<SourceOffer>
{
    public void Configure(EntityTypeBuilder<SourceOffer> b)
    {
        b.ToTable("source_offers");
        b.HasKey(x => x.Id);

        b.HasOne(x => x.SupplierConnection)
            .WithMany()
            .HasForeignKey(x => x.SupplierConnectionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Property(x => x.ExternalId).HasMaxLength(200).IsRequired();
        b.Property(x => x.RawPayload).HasColumnType("jsonb");
        b.Property(x => x.State).HasConversion<string>().HasMaxLength(32);

        // A connector upserts by (connection, external id), so that pair is unique.
        b.HasIndex(x => new { x.SupplierConnectionId, x.ExternalId }).IsUnique();
    }
}
