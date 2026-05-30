using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odisea.Domain.Entities;

namespace Odisea.Infrastructure.Data.Configurations;

public class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> b)
    {
        b.ToTable("offers");
        b.HasKey(x => x.Id);

        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.Country).HasMaxLength(100).IsRequired();
        b.Property(x => x.City).HasMaxLength(150).IsRequired();
        b.Property(x => x.Price).HasColumnType("numeric(12,2)");
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.ImageUrl).HasMaxLength(1000);

        // Enums as strings for readability in the DB.
        b.Property(x => x.OwnerType).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.BoardBasis).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Transport).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);

        // Tags stored as jsonb.
        b.Property(x => x.Tags)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .Metadata.SetValueComparer(JsonValueComparers.StringList);

        b.HasIndex(x => x.Country);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.Visibility);
    }
}
