using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odisea.Modules.Agencies.Domain;

namespace Odisea.Modules.Agencies.Infrastructure.Configurations;

public class AgencyApiKeyConfiguration : IEntityTypeConfiguration<AgencyApiKey>
{
    public void Configure(EntityTypeBuilder<AgencyApiKey> builder)
    {
        builder.Property(k => k.Name).HasMaxLength(100);
        builder.Property(k => k.KeyPrefix).HasMaxLength(16);
        builder.Property(k => k.KeyHash).HasMaxLength(50);
        builder.HasIndex(k => k.KeyHash).IsUnique();
        builder.HasIndex(k => k.AgencyId);
        builder.HasOne<Agency>().WithMany().HasForeignKey(k => k.AgencyId);
    }
}
