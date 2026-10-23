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
public class AuthController(AuthService auth, RefreshTokenService refreshTokens) : ControllerBase
{
    /// HttpOnly + SameSite=Strict + path-scoped: scripts can never read it,
    /// and the browser only sends it to the auth endpoints themselves.
    private const string RefreshCookie = "odisea_refresh";
    private const string CookiePath = "/api/v1/auth";

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        try
        {
            var (response, refresh) = await auth.LoginAsync(request.Email, request.Password, ct);
            SetRefreshCookie(refresh);
            return Ok(response);
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

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Refresh(CancellationToken ct)
    {
        if (Request.Cookies[RefreshCookie] is not { Length: > 0 } rawToken)
            return Problem(title: "No session", statusCode: StatusCodes.Status401Unauthorized);

        try
        {
            var (user, newRefresh) = await refreshTokens.RotateAsync(rawToken, ct);
            SetRefreshCookie(newRefresh);
            return Ok(auth.BuildResponse(user));
        }
        catch (InvalidRefreshTokenException ex)
        {
            ClearRefreshCookie();
            return Problem(title: "Session expired", detail: ex.Message,
                statusCode: StatusCodes.Status401Unauthorized);
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Request.Cookies[RefreshCookie] is { Length: > 0 } rawToken)
            await refreshTokens.RevokeByRawTokenAsync(rawToken, ct);
        ClearRefreshCookie();
        return NoContent();
    }

    private void SetRefreshCookie(string value) =>
        Response.Cookies.Append(RefreshCookie, value, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps, // dev runs plain http on localhost
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
            MaxAge = TimeSpan.FromDays(14),
        });

    private void ClearRefreshCookie() =>
        Response.Cookies.Delete(RefreshCookie, new CookieOptions { Path = CookiePath });
}
