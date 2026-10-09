using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.Features.Agencies;
using Odisea.Modules.Agencies.Infrastructure;
using Odisea.Modules.Agencies.PublicApi;

namespace Odisea.Modules.Agencies.Controllers;

[ApiController]
[Route("api/v1/agencies")]
[Authorize(Policy = AuthPolicies.Operator)]
public class AgenciesController(AgenciesDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AgencyDto>>> List(CancellationToken ct) =>
        Ok(await db.Agencies.OrderBy(a => a.Name).Select(a => a.ToDto()).ToListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AgencyDto>> Get(Guid id, CancellationToken ct)
    {
        var agency = await db.Agencies.FindAsync([id], ct);
        return agency is null ? NotFound() : Ok(agency.ToDto());
    }

    [HttpPost]
    public async Task<ActionResult<AgencyDto>> Create(CreateAgencyRequest request, CancellationToken ct)
    {
        var agency = new Agency
        {
            Name = request.Name,
            Country = request.Country.ToUpperInvariant(),
            CreditLimit = request.CreditLimit,
            ContactEmail = request.ContactEmail,
        };

        db.Agencies.Add(agency);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = agency.Id }, agency.ToDto());
    }

    [HttpPut("{id:guid}/billing")]
    public async Task<ActionResult<AgencyDto>> UpdateBilling(
        Guid id, UpdateAgencyBillingRequest request, CancellationToken ct)
    {
        var agency = await db.Agencies.FindAsync([id], ct);
        if (agency is null)
            return NotFound();

        agency.LegalName = request.LegalName;
        agency.Eik = request.Eik;
        agency.VatNumber = request.VatNumber;
        agency.BillingAddress = request.Address;
        agency.BillingCity = request.City;
        agency.Mol = request.Mol;

        await db.SaveChangesAsync(ct);
        return Ok(agency.ToDto());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AgencyDto>> Update(Guid id, UpdateAgencyRequest request, CancellationToken ct)
    {
        var agency = await db.Agencies.FindAsync([id], ct);
        if (agency is null)
            return NotFound();

        agency.Name = request.Name;
        agency.Country = request.Country.ToUpperInvariant();
        agency.CreditLimit = request.CreditLimit;
        agency.ContactEmail = request.ContactEmail;
        agency.Status = request.Status;

        await db.SaveChangesAsync(ct);
        return Ok(agency.ToDto());
    }
}
