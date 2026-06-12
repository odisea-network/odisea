using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Common.Interfaces;
using Odisea.Application.Leads.Dtos;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;

namespace Odisea.WebAPI.Controllers;

[ApiController]
[Route("api/v1/leads")]
public class LeadsController(IAppDbContext db, IAgencyContext agencyCtx) : ControllerBase
{
    // ── Public ingest (from the embedded od-booking-inquiry surface) ────────────

    // Anonymous + IP rate-limited (reuses the analytics "events" policy). The
    // owning agency is resolved from the publication — never trusted from the
    // client. Unknown publication key → 404 (the real embed always has a valid key;
    // a 404 tells a misconfigured integration it's pointing at nothing).
    [AllowAnonymous]
    [EnableRateLimiting("events")]
    [HttpPost]
    public async Task<IActionResult> Submit(CreateLeadRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.ContactName) || string.IsNullOrWhiteSpace(req.ContactEmail))
            return Problem(title: "Validation", detail: "Contact name and email are required.", statusCode: 400);

        var publication = await db.Publications
            .FirstOrDefaultAsync(p => p.Key == req.PublicationKey, ct);
        if (publication is null)
            return Problem(title: "Not found", detail: "Unknown publication key.", statusCode: 404);

        // A lead tied to a specific offer with party detail is a BookingRequest;
        // otherwise it's a general Inquiry.
        var kind = req.OfferId is not null && req.PartySize is not null
            ? LeadKind.BookingRequest
            : LeadKind.Inquiry;

        var channel = Enum.TryParse<Channel>(req.Channel, ignoreCase: true, out var c)
            ? c
            : Channel.WebComponent;

        var lead = new Lead
        {
            Kind = kind,
            Status = LeadStatus.New,
            AgencyId = publication.AgencyId,
            PublicationKey = req.PublicationKey,
            Channel = channel,
            ContactName = req.ContactName,
            ContactEmail = req.ContactEmail,
            ContactPhone = req.ContactPhone,
            Message = req.Message,
            OfferId = req.OfferId,
            PartySize = req.PartySize,
            PreferredDepartureDate = req.PreferredDepartureDate,
            Nights = req.Nights,
        };
        db.Leads.Add(lead);
        await db.SaveChangesAsync(ct);

        // 202: the traveler's form succeeded; the agency works it from the inbox.
        return Accepted(new { lead.Id });
    }

    // ── Agency inbox ────────────────────────────────────────────────────────────

    [Authorize(Policy = "AgencyMember")]
    [HttpGet]
    public async Task<IActionResult> List(string? status, CancellationToken ct)
    {
        var agencyId = agencyCtx.RequireAgency();

        var q = db.Leads.Where(l => l.AgencyId == agencyId);
        if (Enum.TryParse<LeadStatus>(status, ignoreCase: true, out var s))
            q = q.Where(l => l.Status == s);

        var list = await q.OrderByDescending(l => l.CreatedAt).ToListAsync(ct);
        return Ok(list.Select(l => l.ToDto()));
    }

    [Authorize(Policy = "AgencyMember")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var lead = await FindOwnedLead(id, ct);
        return lead is null ? NotFound() : Ok(lead.ToDto());
    }

    [Authorize(Policy = "AgencyMember")]
    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, UpdateLeadStatusRequest req, CancellationToken ct)
    {
        if (!Enum.TryParse<LeadStatus>(req.Status, ignoreCase: true, out var status))
            return Problem(title: "Validation", detail: $"Invalid status: {req.Status}.", statusCode: 400);

        var lead = await FindOwnedLead(id, ct);
        if (lead is null) return NotFound();

        lead.Status = status;
        lead.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(lead.ToDto());
    }

    // Scopes to the caller's agency; returns null for both "missing" and "not yours"
    // so the inbox never leaks another agency's lead ids.
    private async Task<Lead?> FindOwnedLead(Guid id, CancellationToken ct)
    {
        var agencyId = agencyCtx.RequireAgency();
        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == id, ct);
        return lead is not null && lead.AgencyId == agencyId ? lead : null;
    }
}
