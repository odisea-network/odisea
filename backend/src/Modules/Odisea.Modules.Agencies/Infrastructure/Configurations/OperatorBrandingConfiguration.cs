using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odisea.Modules.Agencies.Domain;

namespace Odisea.Modules.Agencies.Infrastructure.Configurations;

public class OperatorBrandingConfiguration : IEntityTypeConfiguration<OperatorBranding>
{
    public void Configure(EntityTypeBuilder<OperatorBranding> builder)
    {
        builder.ToTable("operator_branding");
        builder.Property(b => b.DisplayName).HasMaxLength(100);
        builder.Property(b => b.PrimaryColor).HasMaxLength(7);
        builder.Property(b => b.AccentColor).HasMaxLength(7);
        builder.Property(b => b.SupportEmail).HasMaxLength(320);
        builder.Property(b => b.SupportPhone).HasMaxLength(30);
        builder.Property(b => b.LegalFooter).HasMaxLength(500);
        builder.Property(b => b.LogoContentType).HasMaxLength(50);
        builder.Property(b => b.LogoHash).HasMaxLength(64);
        builder.Ignore(b => b.HasLogo);
    }
}
