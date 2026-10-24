using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Booking.Domain;

namespace Odisea.Modules.Booking.Infrastructure;

public class BookingDbContext(DbContextOptions<BookingDbContext> options, IDataProtectionProvider dataProtection)
    : DbContext(options)
{
    public const string Schema = "booking";
    public const string RefSequence = "booking_ref_seq";
    public const string DocumentProtectorPurpose = "Odisea.Booking.PassengerDocument";

    public DbSet<Domain.Booking> Bookings => Set<Domain.Booking>();
    public DbSet<BookingPassenger> BookingPassengers => Set<BookingPassenger>();
    public DbSet<BookingPayment> BookingPayments => Set<BookingPayment>();
    public DbSet<BookingStatusHistory> BookingStatusHistory => Set<BookingStatusHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.HasSequence<long>(RefSequence);

        var booking = modelBuilder.Entity<Domain.Booking>();
        booking.Property(b => b.Ref).HasMaxLength(20);
        booking.HasIndex(b => b.Ref).IsUnique();
        booking.HasIndex(b => new { b.AgencyId, b.CreatedAt });
        booking.HasIndex(b => new { b.Status, b.CreatedAt });
        booking.Property(b => b.ProgramName).HasMaxLength(200);
        booking.Property(b => b.HotelName).HasMaxLength(200);
        booking.Property(b => b.ProviderCode).HasMaxLength(30);
        booking.Property(b => b.OfferToken).HasMaxLength(4000);
        booking.Property(b => b.ProviderBookingRef).HasMaxLength(100);
        booking.Property(b => b.Status).HasConversion<string>().HasMaxLength(25);
        booking.Property(b => b.Currency).HasMaxLength(3);
        booking.Property(b => b.NetAmount).HasPrecision(18, 2);
        booking.Property(b => b.SellAmount).HasPrecision(18, 2);
        booking.Property(b => b.CommissionAmount).HasPrecision(18, 2);
        booking.Property(b => b.BreakdownJson).HasColumnType("jsonb");
        booking.HasMany(b => b.Passengers).WithOne().HasForeignKey(p => p.BookingId);
        booking.HasMany(b => b.Payments).WithOne().HasForeignKey(p => p.BookingId);
        booking.HasMany(b => b.History).WithOne().HasForeignKey(h => h.BookingId);

        // Optimistic concurrency rides on Postgres' xmin system column (no column of its own);
        // other providers (the InMemory test double) have nothing equivalent, so skip it there.
        if (Database.IsNpgsql())
            booking.Property(b => b.Version).IsRowVersion();
        else
            booking.Ignore(b => b.Version);

        var passenger = modelBuilder.Entity<BookingPassenger>();
        passenger.Property(p => p.Title).HasMaxLength(10);
        passenger.Property(p => p.Gender).HasConversion<string>().HasMaxLength(10);
        passenger.Property(p => p.FirstName).HasMaxLength(100);
        passenger.Property(p => p.LastName).HasMaxLength(100);
        passenger.Property(p => p.Nationality).HasMaxLength(2);
        passenger.Property(p => p.DocumentType).HasConversion<string>().HasMaxLength(10);
        passenger.Property(p => p.DocumentIssuingCountry).HasMaxLength(2);
        passenger.Property(p => p.Email).HasMaxLength(320);
        passenger.Property(p => p.Phone).HasMaxLength(30);

        // Document numbers are encrypted at rest. EF compares model (plaintext) values, so the
        // non-deterministic ciphertext never causes spurious updates. The model is cached per
        // context type, so the protector captured here belongs to the first instance built —
        // which is fine because IDataProtectionProvider is a singleton.
        // EF never passes null through a converter, hence the null-forgiving operator.
        var documents = dataProtection.CreateProtector(DocumentProtectorPurpose);
        passenger.Property(p => p.DocumentNumber)
            .HasConversion(plain => documents.Protect(plain!), stored => documents.Unprotect(stored))
            .HasMaxLength(512);

        var payment = modelBuilder.Entity<BookingPayment>();
        payment.Property(p => p.Amount).HasPrecision(18, 2);
        payment.Property(p => p.Currency).HasMaxLength(3);
        payment.Property(p => p.Method).HasConversion<string>().HasMaxLength(15);
        payment.Property(p => p.Reference).HasMaxLength(100);
        payment.Property(p => p.Note).HasMaxLength(500);
        payment.Property(p => p.VoidReason).HasMaxLength(300);
        payment.HasIndex(p => p.BookingId);

        var history = modelBuilder.Entity<BookingStatusHistory>();
        history.Property(h => h.FromStatus).HasMaxLength(25);
        history.Property(h => h.ToStatus).HasMaxLength(25);
        history.Property(h => h.ActorType).HasConversion<string>().HasMaxLength(15);
        history.Property(h => h.Note).HasMaxLength(500);
        history.HasIndex(h => h.BookingId);
    }
}
