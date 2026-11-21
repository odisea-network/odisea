using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Workspace.Domain;
using Odisea.Modules.Workspace.Domain.Ledger;

namespace Odisea.Modules.Workspace.Infrastructure;

public class WorkspaceDbContext(DbContextOptions<WorkspaceDbContext> options) : DbContext(options)
{
    public const string Schema = "workspace";

    public DbSet<ExternalReservation> ExternalReservations => Set<ExternalReservation>();
    public DbSet<ExternalReservationEvent> ExternalReservationEvents => Set<ExternalReservationEvent>();
    public DbSet<InquiryForm> InquiryForms => Set<InquiryForm>();
    public DbSet<InquirySubmission> InquirySubmissions => Set<InquirySubmission>();
    public DbSet<TourOperator> TourOperators => Set<TourOperator>();
    public DbSet<LedgerReservation> LedgerReservations => Set<LedgerReservation>();
    public DbSet<LedgerInstallment> LedgerInstallments => Set<LedgerInstallment>();
    public DbSet<LedgerPayment> LedgerPayments => Set<LedgerPayment>();
    public DbSet<LedgerAuditEvent> LedgerAuditEvents => Set<LedgerAuditEvent>();

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

        ConfigureLedger(modelBuilder);
    }

    private void ConfigureLedger(ModelBuilder modelBuilder)
    {
        var tourOperator = modelBuilder.Entity<TourOperator>();
        tourOperator.Property(o => o.Name).HasMaxLength(200);
        tourOperator.Property(o => o.NameKey).HasMaxLength(200);
        tourOperator.Property(o => o.ContactEmail).HasMaxLength(320);
        tourOperator.Property(o => o.ContactPhone).HasMaxLength(30);
        tourOperator.Property(o => o.Notes).HasMaxLength(2000);
        tourOperator.HasIndex(o => new { o.AgencyId, o.NameKey }).IsUnique();

        var reservation = modelBuilder.Entity<LedgerReservation>();
        reservation.Property(r => r.Reference).HasMaxLength(50);
        reservation.Property(r => r.HolderName).HasMaxLength(200);
        reservation.Property(r => r.OperatorTotal).HasPrecision(18, 2);
        reservation.Property(r => r.Currency).HasMaxLength(3);
        reservation.Property(r => r.CustomerSaleAmount).HasPrecision(18, 2);
        reservation.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        reservation.Property(r => r.Notes).HasMaxLength(4000);
        reservation.Property(r => r.CancellationNote).HasMaxLength(500);
        reservation.Property(r => r.CommissionType).HasConversion<string>().HasMaxLength(20);
        reservation.Property(r => r.CommissionRate).HasPrecision(5, 2);
        reservation.Property(r => r.CommissionBase).HasConversion<string>().HasMaxLength(20);
        reservation.Property(r => r.CommissionExpected).HasPrecision(18, 2);
        reservation.Property(r => r.CommissionReceived).HasPrecision(18, 2);
        reservation.HasIndex(r => new { r.AgencyId, r.TourOperatorId, r.Reference }).IsUnique();
        reservation.HasIndex(r => new { r.AgencyId, r.Status });
        reservation.HasIndex(r => new { r.AgencyId, r.TravelStart });
        reservation.HasIndex(r => new { r.AgencyId, r.CreatedAt });
        // An operator with reservations is archived, never deleted.
        reservation.HasOne<TourOperator>().WithMany().HasForeignKey(r => r.TourOperatorId).OnDelete(DeleteBehavior.Restrict);
        reservation.HasMany(r => r.Installments).WithOne().HasForeignKey(i => i.ReservationId).OnDelete(DeleteBehavior.Cascade);
        // Payments are financial records: nothing may take them away with a delete.
        reservation.HasMany(r => r.Payments).WithOne().HasForeignKey(p => p.ReservationId).OnDelete(DeleteBehavior.Restrict);

        // Optimistic concurrency rides on Postgres' xmin system column (no column of its own);
        // the InMemory test double has nothing equivalent, so skip it there.
        if (Database.IsNpgsql())
            reservation.Property(r => r.Version).IsRowVersion();
        else
            reservation.Ignore(r => r.Version);

        var installment = modelBuilder.Entity<LedgerInstallment>();
        installment.Property(i => i.Label).HasConversion<string>().HasMaxLength(20);
        installment.Property(i => i.Amount).HasPrecision(18, 2);
        installment.HasIndex(i => new { i.AgencyId, i.DueDate });

        var payment = modelBuilder.Entity<LedgerPayment>();
        payment.Property(p => p.Amount).HasPrecision(18, 2);
        payment.Property(p => p.Currency).HasMaxLength(3);
        payment.Property(p => p.Method).HasConversion<string>().HasMaxLength(15);
        payment.Property(p => p.Reference).HasMaxLength(100);
        payment.Property(p => p.Note).HasMaxLength(500);
        payment.Property(p => p.VoidReason).HasMaxLength(500);
        payment.HasIndex(p => new { p.ReservationId, p.IdempotencyKey }).IsUnique();

        var audit = modelBuilder.Entity<LedgerAuditEvent>();
        audit.Property(e => e.EntityType).HasMaxLength(30);
        audit.Property(e => e.Action).HasMaxLength(40);
        audit.Property(e => e.ActorType).HasMaxLength(20);
        audit.Property(e => e.Reason).HasMaxLength(500);
        audit.Property(e => e.Changes).HasColumnType("jsonb");
        audit.HasIndex(e => new { e.AgencyId, e.ReservationId, e.CreatedAt });
    }
}
