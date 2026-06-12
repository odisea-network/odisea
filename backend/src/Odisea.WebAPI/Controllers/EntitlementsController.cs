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
public class EntitlementsController(IAppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid? operatorId, Guid? agencyId, CancellationToken ct)
    {
        var q = db.Entitlements.AsQueryable();
        if (operatorId is { } op) q = q.Where(e => e.OperatorId == op);
        if (agencyId is { } ag) q = q.Where(e => e.AgencyId == ag);

        var list = await q.OrderByDescending(e => e.CreatedAt).ToListAsync(ct);
        return Ok(list.Select(e => e.ToDto()));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateEntitlementRequest req, CancellationToken ct)
    {
        if (req.CommissionPercent is < 0 or > 100)
            return Problem(title: "Validation", detail: "CommissionPercent must be between 0 and 100.", statusCode: 400);

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
    public async Task<IActionResult> Suspend(Guid id, CancellationToken ct)
    {
        var entity = await db.Entitlements.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entity is null) return NotFound();

        entity.Status = EntitlementStatus.Suspended;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(entity.ToDto());
    }

    [HttpPost("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
    {
        var entity = await db.Entitlements.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entity is null) return NotFound();

        entity.Status = EntitlementStatus.Active;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(entity.ToDto());
    }
}
