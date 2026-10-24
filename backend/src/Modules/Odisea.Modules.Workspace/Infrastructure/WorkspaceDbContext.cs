using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Workspace.Domain;

namespace Odisea.Modules.Workspace.Infrastructure;

public class WorkspaceDbContext(DbContextOptions<WorkspaceDbContext> options) : DbContext(options)
{
    public const string Schema = "workspace";

    public DbSet<ExternalReservation> ExternalReservations => Set<ExternalReservation>();
    public DbSet<ExternalReservationEvent> ExternalReservationEvents => Set<ExternalReservationEvent>();
    public DbSet<InquiryForm> InquiryForms => Set<InquiryForm>();
    public DbSet<InquirySubmission> InquirySubmissions => Set<InquirySubmission>();

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

        var form = modelBuilder.Entity<InquiryForm>();
        form.Property(f => f.Name).HasMaxLength(100);
        form.Property(f => f.AccessKey).HasMaxLength(40);
        form.Property(f => f.AutoReplySubject).HasMaxLength(200);
        form.Property(f => f.AutoReplyBody).HasMaxLength(4000);
        form.Property(f => f.EmailField).HasMaxLength(60);
        form.Property(f => f.NameField).HasMaxLength(60);
        form.Property(f => f.HoneypotField).HasMaxLength(60);
        form.HasIndex(f => f.AccessKey).IsUnique();
        form.HasIndex(f => f.AgencyId);

        var submission = modelBuilder.Entity<InquirySubmission>();
        submission.Property(s => s.Fields).HasColumnType("jsonb");
        submission.Property(s => s.CustomerName).HasMaxLength(200);
        submission.Property(s => s.CustomerEmail).HasMaxLength(320);
        submission.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        submission.Property(s => s.Origin).HasMaxLength(300);
        submission.Property(s => s.UserAgent).HasMaxLength(300);
        submission.Property(s => s.IpHash).HasMaxLength(64);
        submission.Property(s => s.InternalNote).HasMaxLength(2000);
        submission.HasIndex(s => new { s.AgencyId, s.Status, s.CreatedAt });
        submission.HasIndex(s => s.FormId);
        submission.HasIndex(s => s.CreatedAt);
        // Submissions are the agency's records; a form with submissions is disabled, not deleted.
        submission.HasOne<InquiryForm>().WithMany().HasForeignKey(s => s.FormId).OnDelete(DeleteBehavior.Restrict);
    }
}
