using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Booking.Features.Bookings;
using Odisea.Modules.Booking.Infrastructure;

namespace Odisea.Modules.Booking.Controllers;

/// Operator oversight across all agencies.
[ApiController]
[Route("api/v1/admin/bookings")]
[Authorize(Policy = AuthPolicies.Operator)]
public class OperatorBookingsController(BookingDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<BookingDto>>> List([FromQuery] Guid? agencyId, CancellationToken ct)
    {
        var query = db.Bookings
            .Include(b => b.Passengers)
            .Include(b => b.History)
            .AsNoTracking();
        if (agencyId is { } a)
            query = query.Where(b => b.AgencyId == a);

        var items = await query.OrderByDescending(b => b.CreatedAt).Take(200).ToListAsync(ct);
        return Ok(items.Select(b => b.ToDto()).ToList());
    }
}
