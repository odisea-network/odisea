using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Booking.Domain;
using Odisea.Modules.Booking.Infrastructure;
using Odisea.Modules.Booking.PublicApi;

namespace Odisea.Modules.Booking.Features.Bookings;

public class BookingLookup(BookingDbContext db) : IBookingLookup
{
    public async Task<BookingFinancialInfo?> FindAsync(Guid bookingId, CancellationToken ct)
    {
        var booking = await db.Bookings.AsNoTracking()
            .Include(b => b.History)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct);
        if (booking is null)
            return null;

        return new BookingFinancialInfo(
            booking.Id, booking.Ref, booking.AgencyId, booking.Status.ToString(),
            booking.SellAmount, booking.Currency,
            booking.ProgramName, booking.HotelName, booking.CheckIn, booking.CheckOut,
            ReachedAt(booking, BookingStatus.Confirmed), ReachedAt(booking, BookingStatus.Cancelled));
    }

    public async Task<BookingContractInfo?> GetContractInfoAsync(Guid bookingId, CancellationToken ct)
    {
        var booking = await db.Bookings.AsNoTracking()
            .Include(b => b.History)
            .Include(b => b.Passengers)
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct);
        if (booking is null)
            return null;

        var passengers = booking.Passengers
            .OrderBy(p => p.Position)
            .Select(p => new BookingPassengerInfo(
                p.Position, p.Title, p.Gender?.ToString(), p.FirstName, p.LastName, p.BirthDate,
                p.TypeAt(booking.CheckIn)?.ToString(),
                p.Nationality, p.DocumentType?.ToString(), p.DocumentNumber, p.DocumentIssuingCountry,
                p.DocumentExpiryDate, p.IsLead, p.Email, p.Phone))
            .ToList();

        return new BookingContractInfo(
            booking.Id, booking.Ref, booking.AgencyId, booking.Status.ToString(),
            booking.ProgramName, booking.HotelName, booking.CheckIn, booking.CheckOut,
            booking.Adults, booking.Children,
            booking.SellAmount, booking.CommissionAmount, booking.Currency,
            booking.PaidAmount, booking.BalanceDue, booking.PaymentDueAt,
            booking.CreatedAt, ReachedAt(booking, BookingStatus.Confirmed), passengers);
    }

    private static DateTimeOffset? ReachedAt(Domain.Booking booking, BookingStatus status) => booking.History
        .Where(h => h.ToStatus == status.ToString())
        .OrderByDescending(h => h.CreatedAt)
        .Select(h => (DateTimeOffset?)h.CreatedAt)
        .FirstOrDefault();
}
