using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odisea.Domain.Entities;

namespace Odisea.Infrastructure.Data.Configurations;

public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> b)
    {
        b.ToTable("api_keys");
        b.HasKey(x => x.Id);

        b.Property(x => x.Name).HasMaxLength(128).IsRequired();

        // SHA-256 base64 is 44 chars; lookups hit this index on every API request.
        b.Property(x => x.KeyHash).HasMaxLength(128).IsRequired();
        b.HasIndex(x => x.KeyHash).IsUnique();

        b.Property(x => x.Prefix).HasMaxLength(16).IsRequired();
        b.Property(x => x.Scopes).HasMaxLength(256).IsRequired();

        b.HasIndex(x => x.AgencyId);
    }
}
