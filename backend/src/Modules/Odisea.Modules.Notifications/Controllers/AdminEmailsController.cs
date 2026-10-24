using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Notifications.Domain;
using Odisea.Modules.Notifications.Features.Outbox;
using Odisea.Modules.Notifications.Infrastructure;
using Odisea.Modules.Notifications.PublicApi;
using Odisea.SharedKernel;

namespace Odisea.Modules.Notifications.Controllers;

/// Operator view of the outbox: what went out, what is stuck, and the levers to retry or flush.
[ApiController]
[Route("api/v1/admin/emails")]
[Authorize(Policy = AuthPolicies.Operator)]
public class AdminEmailsController(
    NotificationsDbContext db,
    IEmailOutbox outbox,
    EmailDispatchRun dispatch,
    IEmailTransport transport,
    IClock clock) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<EmailMessageDto>>> List(
        [FromQuery] EmailStatus? status, [FromQuery] Guid? agencyId, [FromQuery] string? category, CancellationToken ct)
    {
        var query = db.EmailMessages.AsNoTracking();
        if (status is { } s)
            query = query.Where(m => m.Status == s);
        if (agencyId is { } a)
            query = query.Where(m => m.AgencyId == a);
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(m => m.Category == category);
        var items = await query.OrderByDescending(m => m.CreatedAt).Take(200).ToListAsync(ct);
        return Ok(items.Select(m => m.ToDto()).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmailMessageDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var message = await db.EmailMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
        return message is null ? NotFound() : Ok(message.ToDetailDto());
    }

    [HttpPost("{id:guid}/retry")]
    public Task<IActionResult> Retry(Guid id, CancellationToken ct) =>
        Execute(async () =>
        {
            var message = await db.EmailMessages.FirstOrDefaultAsync(m => m.Id == id, ct)
                ?? throw new EmailNotFoundException();
            message.Retry(clock.UtcNow);
            await db.SaveChangesAsync(ct);
            return Ok(message.ToDto());
        });

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = AuthPolicies.OperatorAdmin)]
    public Task<IActionResult> Cancel(Guid id, CancellationToken ct) =>
        Execute(async () =>
        {
            var message = await db.EmailMessages.FirstOrDefaultAsync(m => m.Id == id, ct)
                ?? throw new EmailNotFoundException();
            message.Cancel();
            await db.SaveChangesAsync(ct);
            return Ok(message.ToDto());
        });

    /// Runs one dispatch pass right now instead of waiting for the poll.
    [HttpPost("dispatch")]
    [Authorize(Policy = AuthPolicies.OperatorAdmin)]
    public async Task<ActionResult<DispatchResult>> Dispatch(CancellationToken ct) =>
        Ok(await dispatch.RunAsync(ct));

    /// Queues a plain test message so an operator can verify the SMTP configuration end to end.
    [HttpPost("test")]
    [Authorize(Policy = AuthPolicies.OperatorAdmin)]
    public Task<IActionResult> SendTest(SendTestEmailRequest request, CancellationToken ct) =>
        Execute(async () =>
        {
            var id = await outbox.EnqueueAsync(new EmailRequest(
                request.To,
                "Odisea test message",
                $"This message confirms that Odisea can send e-mail through the '{transport.Name}' transport.\nSent at {clock.UtcNow:u}.",
                Category: "test"), ct);
            var message = await db.EmailMessages.AsNoTracking().FirstAsync(m => m.Id == id, ct);
            return CreatedAtAction(nameof(Get), new { id }, message.ToDto());
        });

    private async Task<IActionResult> Execute(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (EmailNotFoundException)
        {
            return NotFound();
        }
        catch (EmailStateException ex)
        {
            return Problem(title: "Invalid state", detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (EmailValidationException ex)
        {
            return Problem(title: "Invalid e-mail", detail: ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }
}
