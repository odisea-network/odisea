using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Booking.Domain;
using Odisea.Modules.Booking.Features.Bookings;
using Odisea.SharedKernel;

namespace Odisea.Modules.Booking.Controllers;

/// Operator oversight across all agencies. Every operator may look, record payments, reject
/// or expire unpaid reservations; confirming, cancelling after payment and voiding a payment
/// are admin decisions (OperatorAdmin policy on top of Operator).
[ApiController]
[Route("api/v1/admin/bookings")]
[Authorize(Policy = AuthPolicies.Operator)]
public class OperatorBookingsController(OperatorBookingService bookings, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List([FromQuery] BookingListQuery query, CancellationToken ct) =>
        Execute(async _ => Ok(await bookings.ListAsync(query, ct)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Execute(async _ => await bookings.GetAsync(id, ct) is { } dto ? Ok(dto) : NotFound());

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Policy = AuthPolicies.OperatorAdmin)]
    public Task<IActionResult> Confirm(Guid id, CancellationToken ct) =>
        Execute(async actor => Ok(await bookings.ConfirmAsync(id, actor, ct)));

    [HttpPost("{id:guid}/reject")]
    public Task<IActionResult> Reject(Guid id, OperatorNoteRequest request, CancellationToken ct) =>
        Execute(async actor => Ok(await bookings.RejectAsync(id, request.Note, actor, ct)));

    [HttpPost("{id:guid}/expire")]
    public Task<IActionResult> Expire(Guid id, CancellationToken ct) =>
        Execute(async actor => Ok(await bookings.ExpireAsync(id, actor, ct)));

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = AuthPolicies.OperatorAdmin)]
    public Task<IActionResult> Cancel(Guid id, OperatorNoteRequest request, CancellationToken ct) =>
        Execute(async actor => Ok(await bookings.CancelAsync(id, request.Note, actor, ct)));

    [HttpPost("{id:guid}/payments")]
    public Task<IActionResult> RecordPayment(Guid id, RecordPaymentRequest request, CancellationToken ct) =>
        Execute(async actor =>
            CreatedAtAction(nameof(Get), new { id }, await bookings.RecordPaymentAsync(id, request, actor, ct)));

    [HttpPost("{id:guid}/payments/{paymentId:guid}/void")]
    [Authorize(Policy = AuthPolicies.OperatorAdmin)]
    public Task<IActionResult> VoidPayment(
        Guid id, Guid paymentId, VoidPaymentRequest request, CancellationToken ct) =>
        Execute(async actor => Ok(await bookings.VoidPaymentAsync(id, paymentId, request.Reason, actor, ct)));

    private async Task<IActionResult> Execute(Func<BookingActor, Task<IActionResult>> action)
    {
        if (currentUser.UserId is not { } userId)
            return Problem(title: "No operator context", statusCode: StatusCodes.Status403Forbidden);

        try
        {
            return await action(new BookingActor(BookingActorType.Operator, userId));
        }
        catch (BookingNotFoundException ex)
        {
            return Problem(title: "Booking not found", detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound);
        }
        catch (PaymentNotFoundException ex)
        {
            return Problem(title: "Payment not found", detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound);
        }
        catch (BookingValidationException ex)
        {
            return Problem(title: "Request rejected", detail: ex.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (PaymentIncompleteException ex)
        {
            return Problem(title: "Not fully paid", detail: ex.Message,
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
