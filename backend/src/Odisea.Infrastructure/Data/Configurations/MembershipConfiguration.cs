using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odisea.Domain.Entities;

namespace Odisea.Infrastructure.Data.Configurations;

public class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> b)
    {
        b.HasKey(m => m.Id);
        b.Property(m => m.TenantType).HasConversion<string>();
        b.Property(m => m.Role).HasConversion<string>();

        // Each user has at most one membership per (tenant type + tenant id) pair.
        b.HasIndex(m => new { m.UserId, m.TenantType, m.TenantId }).IsUnique();
    }
}
