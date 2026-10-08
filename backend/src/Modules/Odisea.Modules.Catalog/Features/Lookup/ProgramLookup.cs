using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Catalog.Domain;
using Odisea.Modules.Catalog.Infrastructure;
using Odisea.Modules.Catalog.PublicApi;

namespace Odisea.Modules.Catalog.Features.Lookup;

public class ProgramLookup(CatalogDbContext db) : IProgramLookup
{
    public async Task<BookingTarget?> ResolveBookingTargetAsync(
        Guid programId, Guid departureId, Guid hotelId, CancellationToken ct)
    {
        var program = await db.Programs.AsNoTracking()
            .Include(p => p.Departures)
            .Include(p => p.Hotels)
            .FirstOrDefaultAsync(p => p.Id == programId && p.Status == ProgramStatus.Published, ct);
        if (program is null)
            return null;

        var departure = program.Departures.FirstOrDefault(d => d.Id == departureId);
        if (departure is null || program.Hotels.All(h => h.HotelId != hotelId))
            return null;

        var hotel = await db.Hotels.AsNoTracking().FirstAsync(h => h.Id == hotelId, ct);
        return new BookingTarget(
            program.Id, program.Name, program.ProviderCode,
            departure.Id, departure.StartDate, departure.EndDate,
            hotel.Id, hotel.Name);
    }
}
