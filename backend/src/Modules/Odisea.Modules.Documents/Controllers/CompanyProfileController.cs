using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Documents.Domain;
using Odisea.Modules.Documents.Features.Issuing;
using Odisea.Modules.Documents.Infrastructure;

namespace Odisea.Modules.Documents.Controllers;

[ApiController]
[Route("api/v1/company-profile")]
[Authorize(Policy = AuthPolicies.Operator)]
public class CompanyProfileController(DocumentsDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CompanyProfileDto>> Get(CancellationToken ct)
    {
        var profile = await db.CompanyProfiles.AsNoTracking().FirstOrDefaultAsync(ct);
        return profile is null ? NotFound() : Ok(profile.ToDto());
    }

    [HttpPut]
    public async Task<ActionResult<CompanyProfileDto>> Save(SaveCompanyProfileRequest request, CancellationToken ct)
    {
        var profile = await db.CompanyProfiles.FirstOrDefaultAsync(ct);
        if (profile is null)
        {
            profile = new CompanyProfile
            {
                Name = request.Name, Eik = request.Eik, Address = request.Address,
                City = request.City, Mol = request.Mol,
            };
            db.CompanyProfiles.Add(profile);
        }

        profile.Name = request.Name;
        profile.Eik = request.Eik;
        profile.VatNumber = request.VatNumber;
        profile.Address = request.Address;
        profile.City = request.City;
        profile.Mol = request.Mol;
        profile.Iban = request.Iban;
        profile.BankName = request.BankName;

        await db.SaveChangesAsync(ct);
        return Ok(profile.ToDto());
    }
}
