using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odisea.Domain.Entities;

namespace Odisea.Infrastructure.Data.Configurations;

public class AllowedDomainConfiguration : IEntityTypeConfiguration<AllowedDomain>
{
    public void Configure(EntityTypeBuilder<AllowedDomain> b)
    {
        b.ToTable("allowed_domains");
        b.HasKey(x => x.Id);

        b.Property(x => x.Domain).HasMaxLength(256).IsRequired();

        // One domain listed once per publication.
        b.HasIndex(x => new { x.PublicationId, x.Domain }).IsUnique();

        b.HasOne(x => x.Publication)
         .WithMany(p => p.AllowedDomains)
         .HasForeignKey(x => x.PublicationId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
