using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Booking.Infrastructure;

namespace Odisea.Modules.Booking.Features.Bookings;

public interface IBookingRefGenerator
{
    Task<string> NextRefAsync(int year, CancellationToken ct);
}

/// Human-readable, gapless-enough refs from a Postgres sequence: ODI-2027-000123.
public class PostgresBookingRefGenerator(BookingDbContext db) : IBookingRefGenerator
{
    public async Task<string> NextRefAsync(int year, CancellationToken ct)
    {
        var next = await db.Database
            .SqlQuery<long>($@"SELECT nextval('booking.booking_ref_seq') AS ""Value""")
            .FirstAsync(ct);
        return $"ODI-{year}-{next:D6}";
    }
}
