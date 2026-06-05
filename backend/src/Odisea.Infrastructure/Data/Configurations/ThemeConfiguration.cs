using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odisea.Domain.Entities;
using Odisea.Domain.ValueObjects;

namespace Odisea.Infrastructure.Data.Configurations;

public class ThemeConfiguration : IEntityTypeConfiguration<Theme>
{
    public void Configure(EntityTypeBuilder<Theme> b)
    {
        b.ToTable("themes");
        b.HasKey(x => x.Id);

        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Version).IsRequired();

        var json = new JsonSerializerOptions();

        b.Property(x => x.Tokens)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, json),
                v => JsonSerializer.Deserialize<ThemeTokens>(v, json) ?? ThemeTokens.Default())
            .Metadata.SetValueComparer(JsonValueComparers.ThemeTokens);

        b.HasIndex(x => x.AgencyId);
        b.HasIndex(x => x.Status);
    }
}
