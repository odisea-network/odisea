using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odisea.Domain.Entities;

namespace Odisea.Infrastructure.Data.Configurations;

public class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> b)
    {
        b.ToTable("leads");
        b.HasKey(x => x.Id);

        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Channel).HasConversion<string>().HasMaxLength(32);

        b.Property(x => x.PublicationKey).HasMaxLength(64).IsRequired();
        b.Property(x => x.ContactName).HasMaxLength(200).IsRequired();
        b.Property(x => x.ContactEmail).HasMaxLength(320).IsRequired();
        b.Property(x => x.ContactPhone).HasMaxLength(64);
        b.Property(x => x.Message).HasMaxLength(4000);

        // Inbox queries: an agency's leads, newest first, often filtered by status.
        b.HasIndex(x => new { x.AgencyId, x.Status, x.CreatedAt });
    }
}
