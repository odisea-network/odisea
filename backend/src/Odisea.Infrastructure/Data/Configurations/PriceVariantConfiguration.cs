using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odisea.Domain.Entities;

namespace Odisea.Infrastructure.Data.Configurations;

public class PriceVariantConfiguration : IEntityTypeConfiguration<PriceVariant>
{
    public void Configure(EntityTypeBuilder<PriceVariant> b)
    {
        b.ToTable("price_variants");
        b.HasKey(x => x.Id);

        b.HasOne(x => x.Offer)
            .WithMany(o => o.PriceVariants)
            .HasForeignKey(x => x.OfferId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Property(x => x.BoardBasis).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Price).HasColumnType("numeric(12,2)");
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();

        // Queries will typically filter by (offer, departure) or (offer, board).
        b.HasIndex(x => new { x.OfferId, x.DepartureDate });
        b.HasIndex(x => new { x.OfferId, x.BoardBasis });
    }
}
