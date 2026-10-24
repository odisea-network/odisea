using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Workspace.Domain;

namespace Odisea.Modules.Workspace.Infrastructure;

public class WorkspaceDbContext(DbContextOptions<WorkspaceDbContext> options) : DbContext(options)
{
    public const string Schema = "workspace";

    public DbSet<ExternalReservation> ExternalReservations => Set<ExternalReservation>();
    public DbSet<ExternalReservationEvent> ExternalReservationEvents => Set<ExternalReservationEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        var reservation = modelBuilder.Entity<ExternalReservation>();
        reservation.Property(r => r.Reference).HasMaxLength(50);
        reservation.Property(r => r.TrackingCode).HasMaxLength(16);
        reservation.Property(r => r.Title).HasMaxLength(200);
        reservation.Property(r => r.Status).HasMaxLength(40);
        reservation.Property(r => r.CustomerName).HasMaxLength(200);
        reservation.Property(r => r.CustomerEmail).HasMaxLength(320);
        reservation.Property(r => r.CustomerPhone).HasMaxLength(30);
        reservation.Property(r => r.Payload).HasColumnType("jsonb");
        reservation.HasIndex(r => new { r.AgencyId, r.Reference }).IsUnique();
        reservation.HasIndex(r => r.TrackingCode).IsUnique();
        reservation.HasIndex(r => new { r.AgencyId, r.Status });
        reservation.HasIndex(r => new { r.AgencyId, r.UpdatedAt });
        reservation.HasIndex(r => r.CreatedAt);
        reservation.HasMany(r => r.Events).WithOne().HasForeignKey(e => e.ReservationId).OnDelete(DeleteBehavior.Cascade);

        var evt = modelBuilder.Entity<ExternalReservationEvent>();
        evt.Property(e => e.Kind).HasConversion<string>().HasMaxLength(20);
        evt.Property(e => e.FromStatus).HasMaxLength(40);
        evt.Property(e => e.ToStatus).HasMaxLength(40);
        evt.Property(e => e.Note).HasMaxLength(2000);
        evt.Property(e => e.ActorType).HasMaxLength(20);
        evt.HasIndex(e => e.ReservationId);
    }
}
