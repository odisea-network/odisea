using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odisea.Domain.Entities;
using Odisea.Domain.ValueObjects;

namespace Odisea.Infrastructure.Data.Configurations;

public class CollectionConfiguration : IEntityTypeConfiguration<Collection>
{
    public void Configure(EntityTypeBuilder<Collection> b)
    {
        b.ToTable("collections");
        b.HasKey(x => x.Id);

        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();

        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);

        var json = new JsonSerializerOptions();

        b.Property(x => x.Filter)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, json),
                v => JsonSerializer.Deserialize<FilterSpec>(v, json) ?? new FilterSpec())
            .Metadata.SetValueComparer(JsonValueComparers.FilterSpec);

        b.Property(x => x.Parameters)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, json),
                v => JsonSerializer.Deserialize<List<ParameterDef>>(v, json) ?? new List<ParameterDef>())
            .Metadata.SetValueComparer(JsonValueComparers.ParameterList);

        b.Property(x => x.PinnedOfferIds)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, json),
                v => JsonSerializer.Deserialize<List<Guid>>(v, json) ?? new List<Guid>())
            .Metadata.SetValueComparer(JsonValueComparers.GuidList);

        b.Property(x => x.ExcludedOfferIds)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, json),
                v => JsonSerializer.Deserialize<List<Guid>>(v, json) ?? new List<Guid>())
            .Metadata.SetValueComparer(JsonValueComparers.GuidList);

        b.Property(x => x.Sort)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, json),
                v => JsonSerializer.Deserialize<SortSpec>(v, json) ?? new SortSpec("price", "asc"))
            .Metadata.SetValueComparer(JsonValueComparers.Sort);
    }
}
