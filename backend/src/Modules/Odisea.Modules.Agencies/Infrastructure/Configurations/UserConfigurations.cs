using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odisea.Modules.Agencies.Domain;

namespace Odisea.Modules.Agencies.Infrastructure.Configurations;

public class AgencyUserConfiguration : IEntityTypeConfiguration<AgencyUser>
{
    public void Configure(EntityTypeBuilder<AgencyUser> builder)
    {
        builder.Property(u => u.Email).HasMaxLength(320);
        builder.Property(u => u.PasswordHash).HasMaxLength(500);
        builder.Property(u => u.FullName).HasMaxLength(200);
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasOne<Agency>().WithMany().HasForeignKey(u => u.AgencyId);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.Property(t => t.TokenHash).HasMaxLength(50);
        builder.Property(t => t.UserType).HasMaxLength(10);
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.FamilyId);
        builder.HasIndex(t => t.UserId);
    }
}

public class OperatorUserConfiguration : IEntityTypeConfiguration<OperatorUser>
{
    public void Configure(EntityTypeBuilder<OperatorUser> builder)
    {
        builder.Property(u => u.Email).HasMaxLength(320);
        builder.Property(u => u.PasswordHash).HasMaxLength(500);
        builder.Property(u => u.FullName).HasMaxLength(200);
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(u => u.Email).IsUnique();
    }
}
