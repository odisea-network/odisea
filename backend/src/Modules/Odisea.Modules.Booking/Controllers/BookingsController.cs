using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        Execute(async (agencyId, actor) =>
        {
            var dto = await bookings.CreateDraftAsync(request, agencyId, actor, ct);
            return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
        });

    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct) =>
        Execute(async (agencyId, _) => Ok(await bookings.ListAsync(agencyId, ct)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Execute(async (agencyId, _) =>
            await bookings.GetAsync(id, agencyId, ct) is { } dto ? Ok(dto) : NotFound());

    [HttpPut("{id:guid}/passengers")]
    public Task<IActionResult> ReplacePassengers(Guid id, UpdatePassengersRequest request, CancellationToken ct) =>
        Execute(async (agencyId, _) => Ok(await bookings.ReplacePassengersAsync(id, agencyId, request, ct)));

    [HttpPost("{id:guid}/re-price")]
    public Task<IActionResult> RePrice(Guid id, CancellationToken ct) =>
        Execute(async (agencyId, actor) => Ok(await bookings.RePriceAsync(id, agencyId, actor, ct)));

    [HttpPost("{id:guid}/submit")]
    public Task<IActionResult> Submit(Guid id, CancellationToken ct) =>
        Execute(async (agencyId, actor) => Ok(await bookings.SubmitAsync(id, agencyId, actor, ct)));

    [HttpPost("{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, CancellationToken ct) =>
        Execute(async (agencyId, actor) => Ok(await bookings.CancelAsync(id, agencyId, actor, ct)));

    /// One funnel for agency scoping and the typed-exception → ProblemDetails map.
    private async Task<IActionResult> Execute(Func<Guid, BookingActor, Task<IActionResult>> action)
    {
        if (currentUser.AgencyId is not { } agencyId || currentUser.UserId is not { } userId)
            return Problem(title: "No agency context", statusCode: StatusCodes.Status403Forbidden);

        try
        {
            var actorType = currentUser.IsApiClient ? BookingActorType.ApiClient : BookingActorType.Agency;
            return await action(agencyId, new BookingActor(actorType, userId));
        }
        catch (BookingNotFoundException ex)
        {
            return Problem(title: "Booking not found", detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound);
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
        catch (BookingStateException ex)
        {
            return Problem(title: "Invalid state", detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (InvalidBookingTransitionException ex)
        {
            return Problem(title: "Invalid state", detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem(title: "Concurrent update",
                detail: "The booking was modified by someone else. Reload it and try again.",
                statusCode: StatusCodes.Status409Conflict);
        }
    }
}
