using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odisea.Domain.Entities;

namespace Odisea.Infrastructure.Data.Configurations;

public class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> b)
    {
        b.ToTable("webhook_subscriptions");
        b.HasKey(x => x.Id);

        b.Property(x => x.Url).HasMaxLength(2048).IsRequired();
        b.Property(x => x.Secret).HasMaxLength(128).IsRequired();
        b.Property(x => x.EventTypes).HasMaxLength(512).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);

        // Dispatch looks up an agency's active subscriptions for an event.
        b.HasIndex(x => new { x.AgencyId, x.Status });
    }
}
