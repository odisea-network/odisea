using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odisea.Domain.Entities;

namespace Odisea.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.HasKey(u => u.Id);
        b.Property(u => u.Email).HasMaxLength(256).IsRequired();
        b.HasIndex(u => u.Email).IsUnique();
        b.Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();
        b.Property(u => u.DisplayName).HasMaxLength(256).IsRequired();
        b.Property(u => u.Status).HasConversion<string>();
        b.Property(u => u.RefreshTokenHash).HasMaxLength(128);

        b.HasMany(u => u.Memberships)
         .WithOne(m => m.User)
         .HasForeignKey(m => m.UserId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
