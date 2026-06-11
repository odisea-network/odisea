using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odisea.Domain.Entities;
using Odisea.Domain.ValueObjects;

namespace Odisea.Infrastructure.Data.Configurations;

public class ExperienceConfiguration : IEntityTypeConfiguration<Experience>
{
    public void Configure(EntityTypeBuilder<Experience> b)
    {
        b.ToTable("experiences");
        b.HasKey(x => x.Id);

        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Version).IsRequired();

        var json = new JsonSerializerOptions();

        b.Property(x => x.Config)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, json),
                v => JsonSerializer.Deserialize<ExperienceConfig>(v, json) ?? new ExperienceConfig())
            .Metadata.SetValueComparer(JsonValueComparers.ExperienceConfig);

        b.HasIndex(x => x.AgencyId);
        b.HasIndex(x => x.Status);
    }
}
