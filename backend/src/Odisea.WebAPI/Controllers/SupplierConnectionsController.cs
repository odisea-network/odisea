using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Common.Interfaces;
using Odisea.Application.Suppliers.Connectors;
using Odisea.Application.Suppliers.Dtos;
using Odisea.Application.Suppliers.Freshness;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;

namespace Odisea.WebAPI.Controllers;

[ApiController]
[Route("api/v1/supplier-connections")]
[Authorize(Policy = "OperatorAdmin")]
public class SupplierConnectionsController(
    IAppDbContext db,
    IImportRunner importRunner,
    IFreshnessService freshness,
    IOperatorContext operatorCtx) : ControllerBase
{
    // Last N runs surfaced per connection on the health rollup.
    private const int RecentRunWindow = 20;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var operatorId = operatorCtx.RequireOperator();
        var list = await db.SupplierConnections
            .Where(c => c.OperatorId == operatorId)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
        return Ok(list.Select(c => c.ToDto()));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var c = await FindOwned(id, ct);
        return c is null ? NotFound() : Ok(c.ToDto());
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateSupplierConnectionRequest req, CancellationToken ct)
    {
        var operatorId = operatorCtx.RequireOperator();

        if (string.IsNullOrWhiteSpace(req.Name))
            return Problem(title: "Validation", detail: "Name is required.", statusCode: 400);

        if (!Enum.TryParse<SupplierConnectionKind>(req.Kind, ignoreCase: true, out var kind))
            return Problem(title: "Validation", detail: $"Invalid kind: {req.Kind}.", statusCode: 400);

        if (!IsValidJson(req.ConfigJson, out var configJson))
            return Problem(title: "Validation", detail: "ConfigJson must be a valid JSON object.", statusCode: 400);

        if (req.FreshnessTtlHours is <= 0)
            return Problem(title: "Validation", detail: "FreshnessTtlHours must be positive.", statusCode: 400);

        var connection = new SupplierConnection
        {
            OperatorId = operatorId,
            Kind = kind,
            Name = req.Name.Trim(),
            ConfigJson = configJson,
            Status = SupplierConnectionStatus.Active,
            FreshnessTtlHours = req.FreshnessTtlHours ?? 24,
        };
        db.SupplierConnections.Add(connection);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = connection.Id }, connection.ToDto());
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateSupplierConnectionRequest req, CancellationToken ct)
    {
        var connection = await FindOwned(id, ct);
        if (connection is null) return NotFound();

        if (req.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Problem(title: "Validation", detail: "Name cannot be blank.", statusCode: 400);
            connection.Name = req.Name.Trim();
        }

        if (req.ConfigJson is not null)
        {
            if (!IsValidJson(req.ConfigJson, out var configJson))
                return Problem(title: "Validation", detail: "ConfigJson must be a valid JSON object.", statusCode: 400);
            connection.ConfigJson = configJson;
        }

        if (req.Status is not null)
        {
            if (!Enum.TryParse<SupplierConnectionStatus>(req.Status, ignoreCase: true, out var status))
                return Problem(title: "Validation", detail: $"Invalid status: {req.Status}.", statusCode: 400);
            connection.Status = status;
        }

        if (req.FreshnessTtlHours is { } ttl)
        {
            if (ttl <= 0)
                return Problem(title: "Validation", detail: "FreshnessTtlHours must be positive.", statusCode: 400);
            connection.FreshnessTtlHours = ttl;
        }

        connection.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(connection.ToDto());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var connection = await FindOwned(id, ct);
        if (connection is null) return NotFound();

        db.SupplierConnections.Remove(connection);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // Triggers the connector for this connection and records an ImportJob.
    [HttpPost("{id:guid}/run")]
    public async Task<IActionResult> Run(Guid id, CancellationToken ct)
    {
        var connection = await FindOwned(id, ct);
        if (connection is null) return NotFound();

        var job = await importRunner.RunAsync(connection, ct);
        return Ok(job.ToDto());
    }

    // Soft-expire stale offers on this connection (LastSeenAt older than the
    // connection's freshness TTL). Returns how many source records and offers
    // were marked Stale. Idempotent — a second sweep with nothing newly stale
    // returns zeroes.
    [HttpPost("{id:guid}/sweep")]
    public async Task<IActionResult> Sweep(Guid id, CancellationToken ct)
    {
        if (await FindOwned(id, ct) is null) return NotFound();

        var result = await freshness.SweepAsync(id, ct);
        return Ok(result);
    }

    // Run history for one connection, newest first — the dead-letter view reads
    // failed jobs' Errors from here.
    [HttpGet("{id:guid}/jobs")]
    public async Task<IActionResult> Jobs(Guid id, CancellationToken ct)
    {
        if (await FindOwned(id, ct) is null) return NotFound();

        var jobs = await db.ImportJobs
            .Where(j => j.SupplierConnectionId == id)
            .OrderByDescending(j => j.StartedAt)
            .Take(RecentRunWindow)
            .ToListAsync(ct);

        return Ok(jobs.Select(j => j.ToDto()));
    }

    // Supplier-health dashboard rollup: one row per connection with freshness and
    // a recent error trend.
    [HttpGet("health")]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        var operatorId = operatorCtx.RequireOperator();
        var connections = await db.SupplierConnections
            .Where(c => c.OperatorId == operatorId)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        var ids = connections.Select(c => c.Id).ToList();

        // Pull the recent jobs for all connections in one query, then roll up
        // in memory — avoids an N+1 across connections.
        var recentJobs = await db.ImportJobs
            .Where(j => ids.Contains(j.SupplierConnectionId))
            .OrderByDescending(j => j.StartedAt)
            .Take(ids.Count * RecentRunWindow)
            .ToListAsync(ct);

        var byConnection = recentJobs
            .GroupBy(j => j.SupplierConnectionId)
            .ToDictionary(g => g.Key, g => g.Take(RecentRunWindow).ToList());

        var health = connections.Select(c =>
        {
            byConnection.TryGetValue(c.Id, out var jobs);
            jobs ??= [];

            var lastRun = jobs.FirstOrDefault();
            var lastSuccess = jobs.FirstOrDefault(j => j.Status == ImportJobStatus.Succeeded);

            return new ConnectionHealthDto(
                c.Id, c.Name, c.Kind.ToString(), c.Status.ToString(),
                c.LastSyncedAt,
                lastSuccess?.CompletedAt,
                lastRun?.Status.ToString(),
                jobs.Count,
                jobs.Count(j => j.Status == ImportJobStatus.Failed));
        });

        return Ok(health);
    }

    // Loads a connection only if it belongs to the calling operator. Returns null
    // for both "missing" and "not yours" so we never leak another operator's data.
    private async Task<SupplierConnection?> FindOwned(Guid id, CancellationToken ct)
    {
        var operatorId = operatorCtx.RequireOperator();
        var connection = await db.SupplierConnections.FirstOrDefaultAsync(c => c.Id == id, ct);
        return connection is not null && connection.OperatorId == operatorId ? connection : null;
    }

    // Validates an optional config blob, defaulting null/blank to an empty object.
    // The blob must be a JSON object so connectors can read settings off it.
    private static bool IsValidJson(string? input, out string normalized)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            normalized = "{}";
            return true;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(input);
            normalized = input;
            return doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object;
        }
        catch (System.Text.Json.JsonException)
        {
            normalized = "{}";
            return false;
        }
    }
}
