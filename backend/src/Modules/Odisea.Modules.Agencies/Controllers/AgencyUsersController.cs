using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.Features.Users;
using Odisea.Modules.Agencies.Infrastructure;
using Odisea.Modules.Agencies.PublicApi;

namespace Odisea.Modules.Agencies.Controllers;

[ApiController]
[Route("api/v1/agencies/{agencyId:guid}/users")]
[Authorize(Policy = AuthPolicies.Operator)]
public class AgencyUsersController(
    AgenciesDbContext db,
    IPasswordHasher<UserAccount> hasher) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AgencyUserDto>>> List(Guid agencyId, CancellationToken ct)
    {
        if (!await db.Agencies.AnyAsync(a => a.Id == agencyId, ct))
            return NotFound();

        return Ok(await db.AgencyUsers
            .Where(u => u.AgencyId == agencyId)
            .OrderBy(u => u.FullName)
            .Select(u => u.ToDto())
            .ToListAsync(ct));
    }

    [HttpPost]
    public async Task<ActionResult<AgencyUserDto>> Create(
        Guid agencyId, CreateAgencyUserRequest request, CancellationToken ct)
    {
        if (!await db.Agencies.AnyAsync(a => a.Id == agencyId, ct))
            return NotFound();

        var email = request.Email.Trim().ToLowerInvariant();
        var emailTaken =
            await db.AgencyUsers.AnyAsync(u => u.Email == email, ct) ||
            await db.OperatorUsers.AnyAsync(u => u.Email == email, ct);
        if (emailTaken)
            return Problem(title: "Duplicate email",
                detail: new DuplicateEmailException(email).Message,
                statusCode: StatusCodes.Status409Conflict);

        var user = new AgencyUser
        {
            AgencyId = agencyId,
            Email = email,
            PasswordHash = "",
            FullName = request.FullName,
            Role = request.Role,
        };
        user.PasswordHash = hasher.HashPassword(user, request.Password);

        db.AgencyUsers.Add(user);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), new { agencyId }, user.ToDto());
    }
}
