using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Booking.Domain;
using Odisea.Modules.Booking.Features.Bookings;
using Odisea.Modules.Integrations.PublicApi;
using Odisea.SharedKernel;

namespace Odisea.Modules.Booking.Controllers;

[ApiController]
[Route("api/v1/bookings")]
[Authorize(Policy = AuthPolicies.Agency)]
public class BookingsController(BookingService bookings, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    public Task<IActionResult> Create(CreateBookingRequest request, CancellationToken ct) =>
        Execute(async (agencyId, userId) =>
            CreatedAtAction(nameof(Get), new { id = Guid.Empty },
                await bookings.CreateDraftAsync(request, agencyId, userId, ct)));

    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct) =>
        Execute(async (agencyId, _) => Ok(await bookings.ListAsync(agencyId, ct)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Execute(async (agencyId, _) =>
            await bookings.GetAsync(id, agencyId, ct) is { } dto ? Ok(dto) : NotFound());

    [HttpPost("{id:guid}/re-price")]
    public Task<IActionResult> RePrice(Guid id, CancellationToken ct) =>
        Execute(async (agencyId, _) => Ok(await bookings.RePriceAsync(id, agencyId, ct)));

    [HttpPost("{id:guid}/confirm")]
    public Task<IActionResult> Confirm(Guid id, CancellationToken ct) =>
        Execute(async (agencyId, _) => Ok(await bookings.ConfirmAsync(id, agencyId, ct)));

    [HttpPost("{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, CancellationToken ct) =>
        Execute(async (agencyId, _) => Ok(await bookings.CancelAsync(id, agencyId, ct)));

    /// One funnel for agency scoping and the typed-exception → ProblemDetails map.
    private async Task<IActionResult> Execute(Func<Guid, Guid, Task<IActionResult>> action)
    {
        if (currentUser.AgencyId is not { } agencyId || currentUser.UserId is not { } userId)
            return Problem(title: "No agency context", statusCode: StatusCodes.Status403Forbidden);

        try
        {
            return await action(agencyId, userId);
        }
        catch (BookingValidationException ex)
        {
            return Problem(title: "Booking rejected", detail: ex.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (InvalidOfferTokenException ex)
        {
            return Problem(title: "Invalid offer token", detail: ex.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (OfferUnavailableException ex)
        {
            return Problem(title: "Offer unavailable", detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (InvalidBookingTransitionException ex)
        {
            return Problem(title: "Invalid state", detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
    }
}
