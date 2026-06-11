using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odisea.Domain.Entities;
using Odisea.Domain.ValueObjects;

namespace Odisea.Infrastructure.Data.Configurations;

public class PublicationConfiguration : IEntityTypeConfiguration<Publication>
{
    public void Configure(EntityTypeBuilder<Publication> b)
    {
        b.ToTable("publications");
        b.HasKey(x => x.Id);

        b.Property(x => x.Key).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.Key).IsUnique();

        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);

        b.Property(x => x.Version).HasDefaultValue(0);

        // AllowedDomains is a child table (allowed_domains) configured in
        // AllowedDomainConfiguration — see issue #27.

        var json = new JsonSerializerOptions();

        b.Property(x => x.ExperienceConfig)
            .HasColumnType("jsonb")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, json),
                v => v == null ? null : JsonSerializer.Deserialize<ExperienceConfig>(v, json));
    }
}
