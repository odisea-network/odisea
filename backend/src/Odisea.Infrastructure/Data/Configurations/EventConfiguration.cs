using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odisea.Domain.Entities;

namespace Odisea.Infrastructure.Data.Configurations;

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> b)
    {
        b.ToTable("events");
        b.HasKey(x => x.Id);

        b.Property(x => x.EventType).HasConversion<string>().HasMaxLength(32).IsRequired();
        b.Property(x => x.Channel).HasConversion<string>().HasMaxLength(32).IsRequired();

        b.Property(x => x.PublicationKey).HasMaxLength(64).IsRequired();
        b.Property(x => x.UserAgentHash).HasMaxLength(64);
        b.Property(x => x.IpHash).HasMaxLength(64);

        // Primary query path: stats for a publication over a time window.
        b.HasIndex(x => new { x.PublicationKey, x.OccurredAt });
    }
}
