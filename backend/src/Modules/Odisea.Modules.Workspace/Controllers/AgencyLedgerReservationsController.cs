using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Workspace.Domain.Ledger;
using Odisea.Modules.Workspace.Features.Ledger;
using Odisea.SharedKernel;

namespace Odisea.Modules.Workspace.Controllers;

/// The agency's reservation ledger: bookings made with any tour operator, their payment schedule,
/// the payments actually sent and the expected commission. Always the caller's agency.
[ApiController]
[Route("api/v1/agency/ledger/reservations")]
[Authorize(Policy = AuthPolicies.Agency)]
public class AgencyLedgerReservationsController(LedgerReservationService ledger, ICurrentUser currentUser)
    : LedgerControllerBase(currentUser)
{
    [HttpGet]
    public Task<IActionResult> List(
        [FromQuery] string? q, [FromQuery] LedgerReservationStatus? status, [FromQuery] Guid? operatorId,
        [FromQuery] DateOnly? travelFrom, [FromQuery] DateOnly? travelTo, [FromQuery] string? sort,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        Execute(async (agencyId, _) => Ok(await ledger.ListAsync(
            agencyId, q, status, operatorId, travelFrom, travelTo, sort, page, pageSize, ct)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Execute(async (agencyId, _) => Ok(await ledger.GetAsync(id, agencyId, ct)));

    [HttpPost]
    public Task<IActionResult> Create(CreateLedgerReservationRequest request, CancellationToken ct) =>
        Execute(async (agencyId, actor) =>
        {
            var dto = await ledger.CreateAsync(agencyId, actor, request, ct);
            return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
        });

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, UpdateLedgerReservationRequest request, CancellationToken ct) =>
        Execute(async (agencyId, actor) => Ok(await ledger.UpdateAsync(id, agencyId, actor, request, ct)));

    [HttpPut("{id:guid}/schedule")]
    public Task<IActionResult> ReplaceSchedule(Guid id, ReplaceScheduleRequest request, CancellationToken ct) =>
        Execute(async (agencyId, actor) => Ok(await ledger.ReplaceScheduleAsync(id, agencyId, actor, request, ct)));

    [HttpPost("{id:guid}/payments")]
    public Task<IActionResult> RecordPayment(Guid id, RecordLedgerPaymentRequest request, CancellationToken ct) =>
        Execute(async (agencyId, actor) => Ok(await ledger.RecordPaymentAsync(id, agencyId, actor, request, ct)));

    [HttpPost("{id:guid}/payments/{paymentId:guid}/void")]
    [Authorize(Policy = AuthPolicies.AgencyAdmin)]
    public Task<IActionResult> VoidPayment(Guid id, Guid paymentId, VoidLedgerPaymentRequest request, CancellationToken ct) =>
        Execute(async (agencyId, actor) => Ok(await ledger.VoidPaymentAsync(id, paymentId, agencyId, actor, request, ct)));

    [HttpPost("{id:guid}/status")]
    public Task<IActionResult> ChangeStatus(Guid id, ChangeLedgerStatusRequest request, CancellationToken ct) =>
        Execute(async (agencyId, actor) => Ok(await ledger.ChangeStatusAsync(id, agencyId, actor, request, ct)));

    [HttpPut("{id:guid}/commission")]
    public Task<IActionResult> SetCommissionReceived(Guid id, SetCommissionReceivedRequest request, CancellationToken ct) =>
        Execute(async (agencyId, actor) => Ok(await ledger.SetCommissionReceivedAsync(id, agencyId, actor, request, ct)));

    [HttpGet("{id:guid}/history")]
    public Task<IActionResult> History(Guid id, CancellationToken ct) =>
        Execute(async (agencyId, _) => Ok(await ledger.HistoryAsync(id, agencyId, ct)));
}
