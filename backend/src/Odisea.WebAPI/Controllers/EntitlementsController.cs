using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Common.Interfaces;
using Odisea.Application.Suppliers.Dtos;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;

namespace Odisea.WebAPI.Controllers;

// Operator → agency commercial access. An Active entitlement lets an agency
// distribute the operator's PlatformShared offers; suspending it cuts that access
// without deleting the commission history.
[ApiController]
[Route("api/v1/entitlements")]
[Authorize(Policy = "OperatorAdmin")]
public class EntitlementsController(IAppDbContext db, IOperatorContext operatorCtx) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid? operatorId, Guid? agencyId, CancellationToken ct)
    {
        var q = db.Entitlements.AsQueryable();

        // An operator only ever sees its own commercial terms; a platform admin
        // (no operator tenant) may filter by the optional operatorId.
        if (operatorCtx.OperatorId is Guid callerOp)
            q = q.Where(e => e.OperatorId == callerOp);
        else if (operatorId is { } op)
            q = q.Where(e => e.OperatorId == op);

        if (agencyId is { } ag) q = q.Where(e => e.AgencyId == ag);

        var list = await q.OrderByDescending(e => e.CreatedAt).ToListAsync(ct);
        return Ok(list.Select(e => e.ToDto()));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateEntitlementRequest req, CancellationToken ct)
    {
        if (req.CommissionPercent is < 0 or > 100)
            return Problem(title: "Validation", detail: "CommissionPercent must be between 0 and 100.", statusCode: 400);

        // An operator can only grant access to its own offers; it can't create
        // entitlements on another operator's behalf.
        if (operatorCtx.OperatorId is Guid callerOp && req.OperatorId != callerOp)
            return Problem(title: "Forbidden", detail: "Cannot create entitlements for another operator.", statusCode: 403);

        if (!await db.Operators.AnyAsync(o => o.Id == req.OperatorId, ct))
            return Problem(title: "Validation", detail: $"Operator {req.OperatorId} not found.", statusCode: 400);

        if (!await db.Agencies.AnyAsync(a => a.Id == req.AgencyId, ct))
            return Problem(title: "Validation", detail: $"Agency {req.AgencyId} not found.", statusCode: 400);

        if (await db.Entitlements.AnyAsync(e => e.OperatorId == req.OperatorId && e.AgencyId == req.AgencyId, ct))
            return Problem(title: "Conflict", detail: "An entitlement already exists for this operator/agency pair.", statusCode: 409);

        var entity = new OperatorAgencyEntitlement
        {
            OperatorId = req.OperatorId,
            AgencyId = req.AgencyId,
            CommissionPercent = req.CommissionPercent,
            Status = EntitlementStatus.Active,
        };
        db.Entitlements.Add(entity);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(List), new { operatorId = entity.OperatorId }, entity.ToDto());
    }

    [HttpPost("{id:guid}/suspend")]
    public Task<IActionResult> Suspend(Guid id, CancellationToken ct) =>
        SetStatus(id, EntitlementStatus.Suspended, ct);

    [HttpPost("{id:guid}/reactivate")]
    public Task<IActionResult> Reactivate(Guid id, CancellationToken ct) =>
        SetStatus(id, EntitlementStatus.Active, ct);

    private async Task<IActionResult> SetStatus(Guid id, EntitlementStatus status, CancellationToken ct)
    {
        var entity = await FindOwned(id, ct);
        if (entity is null) return NotFound();

        entity.Status = status;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(entity.ToDto());
    }

    // Loads an entitlement only if it belongs to the calling operator (platform
    // admins may touch any). 404 for both missing and not-owned — no existence leak.
    private async Task<OperatorAgencyEntitlement?> FindOwned(Guid id, CancellationToken ct)
    {
        var entity = await db.Entitlements.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entity is null) return null;
        if (operatorCtx.OperatorId is Guid callerOp && entity.OperatorId != callerOp) return null;
        return entity;
    }
}
