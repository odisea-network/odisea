using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Notifications.Domain;

namespace Odisea.Modules.Notifications.Infrastructure;

public class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options)
{
    public const string Schema = "notifications";

    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        var email = modelBuilder.Entity<EmailMessage>();
        email.Property(m => m.To).HasMaxLength(320);
        email.Property(m => m.ToName).HasMaxLength(200);
        email.Property(m => m.ReplyTo).HasMaxLength(320);
        email.Property(m => m.Subject).HasMaxLength(200);
        email.Property(m => m.Category).HasMaxLength(50);
        email.Property(m => m.CorrelationId).HasMaxLength(100);
        email.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
        email.Property(m => m.LastError).HasMaxLength(1000);
        // The dispatcher's query: due, pending messages in order.
        email.HasIndex(m => new { m.Status, m.NextAttemptAt });
        email.HasIndex(m => m.AgencyId);
        email.HasIndex(m => m.CorrelationId);
    }
}
