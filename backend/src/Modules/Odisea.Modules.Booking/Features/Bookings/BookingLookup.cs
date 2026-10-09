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

        DateTimeOffset? at(BookingStatus status) => booking.History
            .Where(h => h.ToStatus == status.ToString())
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => (DateTimeOffset?)h.CreatedAt)
            .FirstOrDefault();

        return new BookingFinancialInfo(
            booking.Id, booking.Ref, booking.AgencyId, booking.Status.ToString(),
            booking.SellAmount, booking.Currency,
            booking.ProgramName, booking.HotelName, booking.CheckIn, booking.CheckOut,
            at(BookingStatus.Confirmed), at(BookingStatus.Cancelled));
    }
}
