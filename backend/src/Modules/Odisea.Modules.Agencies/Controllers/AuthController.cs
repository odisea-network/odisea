using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.Features.Auth;

namespace Odisea.Modules.Agencies.Controllers;

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
public class AuthController(AuthService auth) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await auth.LoginAsync(request.Email, request.Password, ct));
        }
        catch (InvalidCredentialsException ex)
        {
            return Problem(title: "Login failed", detail: ex.Message,
                statusCode: StatusCodes.Status401Unauthorized);
        }
        catch (AccountLockedException ex)
        {
            // Deliberately no unlock timestamp in the response — don't hand attackers a schedule.
            return Problem(title: "Account locked", detail: ex.Message,
                statusCode: StatusCodes.Status423Locked);
        }
    }
}
