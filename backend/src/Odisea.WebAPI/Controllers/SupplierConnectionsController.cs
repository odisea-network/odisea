using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Common.Interfaces;
using Odisea.Application.Suppliers.Connectors;
using Odisea.Application.Suppliers.Dtos;

namespace Odisea.WebAPI.Controllers;

[ApiController]
[Route("api/v1/supplier-connections")]
[Authorize(Policy = "OperatorAdmin")]
public class SupplierConnectionsController(
    IAppDbContext db,
    IConnectorRegistry connectors) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var list = await db.SupplierConnections
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
        return Ok(list.Select(c => c.ToDto()));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var c = await db.SupplierConnections.FirstOrDefaultAsync(x => x.Id == id, ct);
        return c is null ? NotFound() : Ok(c.ToDto());
    }

    // Triggers the connector for this connection: fetch → parse → validate →
    // normalize → upsert → deactivate. The Manual adapter is a no-op stub;
    // XML/JSON/CSV adapters land in follow-up PRs.
    [HttpPost("{id:guid}/run")]
    public async Task<IActionResult> Run(Guid id, CancellationToken ct)
    {
        var connection = await db.SupplierConnections.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (connection is null) return NotFound();

        var connector = connectors.For(connection.Kind);
        var result = await connector.RunAsync(connection, ct);

        if (result.Succeeded)
        {
            connection.LastSyncedAt = result.RanAt;
            connection.UpdatedAt = result.RanAt;
            await db.SaveChangesAsync(ct);
        }

        return Ok(result.ToDto());
    }
}
