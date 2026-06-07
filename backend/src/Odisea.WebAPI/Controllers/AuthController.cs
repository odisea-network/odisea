using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Auth.Dtos;
using Odisea.Application.Common.Interfaces;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;

namespace Odisea.WebAPI.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(
    IAppDbContext db,
    IJwtService jwt,
    IPasswordHasherService hasher,
    IHostEnvironment env) : ControllerBase
{
    private const int RefreshTokenExpiryDays = 7;

    // Cookie names used by the portal cookie-auth endpoints.
    private const string AccessCookie  = "od_at";
    private const string RefreshCookie = "od_rt";

    // Scope the refresh cookie to its endpoint so the browser only sends it there.
    private const string RefreshCookiePath = "/api/v1/auth/cookie/refresh";

    // ── Bearer endpoints (API consumers, embed widget) ─────────────────────────

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return Problem(title: "Validation", detail: "Email and password are required.", statusCode: 400);

        if (req.Password.Length < 8)
            return Problem(title: "Validation", detail: "Password must be at least 8 characters.", statusCode: 400);

        var email = req.Email.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return Problem(title: "Conflict", detail: "An account with this email already exists.", statusCode: 409);

        var user = new User
        {
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? email : req.DisplayName.Trim(),
            PasswordHash = hasher.Hash(req.Password),
            Status = UserStatus.Active,
        };

        db.Users.Add(user);

        Membership? membership = null;

        if (req.AgencyId.HasValue)
        {
            if (!await db.Agencies.AnyAsync(a => a.Id == req.AgencyId.Value, ct))
                return Problem(title: "Validation", detail: $"Agency {req.AgencyId} not found.", statusCode: 400);

            membership = new Membership
            {
                UserId = user.Id,
                TenantType = TenantType.Agency,
                TenantId = req.AgencyId,
                Role = UserRole.AgencyAdmin,
            };
            db.Memberships.Add(membership);
        }
        else if (req.OperatorId.HasValue)
        {
            if (!await db.Operators.AnyAsync(o => o.Id == req.OperatorId.Value, ct))
                return Problem(title: "Validation", detail: $"Operator {req.OperatorId} not found.", statusCode: 400);

            membership = new Membership
            {
                UserId = user.Id,
                TenantType = TenantType.Operator,
                TenantId = req.OperatorId,
                Role = UserRole.OperatorAdmin,
            };
            db.Memberships.Add(membership);
        }

        await db.SaveChangesAsync(ct);

        var (accessToken, expiresAt, rawRefresh) = await IssueTokensAsync(user, membership, ct);
        return Ok(new TokenResponse(accessToken, rawRefresh, expiresAt));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req, CancellationToken ct)
    {
        var (user, membership, error) = await ValidateCredentialsAsync(req.Email, req.Password, ct);
        if (error is not null) return error;

        var (accessToken, expiresAt, rawRefresh) = await IssueTokensAsync(user!, membership, ct);
        return Ok(new TokenResponse(accessToken, rawRefresh, expiresAt));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.RefreshToken))
            return Problem(title: "Validation", detail: "Refresh token is required.", statusCode: 400);

        var (user, membership, error) = await RotateRefreshTokenAsync(req.RefreshToken, ct);
        if (error is not null) return error;

        var (accessToken, expiresAt, rawRefresh) = await IssueTokensAsync(user!, membership, ct);
        return Ok(new TokenResponse(accessToken, rawRefresh, expiresAt));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr is null || !Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var user = await db.Users
            .Include(u => u.Memberships)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null) return NotFound();

        var membership = user.Memberships.FirstOrDefault();
        return Ok(new CurrentUserDto(
            user.Id,
            user.Email,
            user.DisplayName,
            membership?.Role.ToString() ?? UserRole.PlatformAdmin.ToString(),
            membership?.TenantType.ToString(),
            membership?.TenantId));
    }

    // ── Cookie endpoints (Angular portal) ─────────────────────────────────────
    // Credentials-enabled CORS is scoped to these endpoints only via PortalCors.
    // Both tokens are set as HttpOnly + Secure + SameSite=Strict cookies;
    // JavaScript cannot read them even if the page is XSS-compromised.

    [EnableCors("PortalCors")]
    [HttpPost("cookie/login")]
    public async Task<IActionResult> CookieLogin(LoginRequest req, CancellationToken ct)
    {
        var (user, membership, error) = await ValidateCredentialsAsync(req.Email, req.Password, ct);
        if (error is not null) return error;

        var (accessToken, expiresAt, rawRefresh) = await IssueTokensAsync(user!, membership, ct);
        SetAuthCookies(accessToken, expiresAt, rawRefresh);

        return Ok(new { expiresAt });
    }

    [EnableCors("PortalCors")]
    [HttpPost("cookie/refresh")]
    public async Task<IActionResult> CookieRefresh(CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookie, out var rawRefresh) ||
            string.IsNullOrEmpty(rawRefresh))
        {
            return Problem(title: "Unauthorized", detail: "No refresh token cookie.", statusCode: 401);
        }

        var (user, membership, error) = await RotateRefreshTokenAsync(rawRefresh, ct);
        if (error is not null)
        {
            ClearAuthCookies();
            return error;
        }

        var (accessToken, expiresAt, newRawRefresh) = await IssueTokensAsync(user!, membership, ct);
        SetAuthCookies(accessToken, expiresAt, newRawRefresh);

        return Ok(new { expiresAt });
    }

    [EnableCors("PortalCors")]
    [Authorize]
    [HttpPost("cookie/logout")]
    public IActionResult CookieLogout()
    {
        ClearAuthCookies();
        return Ok();
    }

    // ── Shared helpers ─────────────────────────────────────────────────────────

    // Validates email + password. Always runs hash comparison to prevent timing-based
    // email enumeration even when the user record doesn't exist.
    private async Task<(User? user, Membership? membership, IActionResult? error)>
        ValidateCredentialsAsync(string rawEmail, string password, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawEmail) || string.IsNullOrWhiteSpace(password))
            return (null, null, Problem(title: "Validation", detail: "Email and password are required.", statusCode: 400));

        var email = rawEmail.Trim().ToLowerInvariant();

        var user = await db.Users
            .Include(u => u.Memberships)
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        if (!hasher.Verify(user?.PasswordHash, password))
            return (null, null, Problem(title: "Unauthorized", detail: "Invalid email or password.", statusCode: 401));

        if (user!.Status == UserStatus.Suspended)
            return (null, null, Problem(title: "Forbidden", detail: "Account is suspended.", statusCode: 403));

        return (user, user.Memberships.FirstOrDefault(), null);
    }

    // Looks up the user by hashed refresh token and validates expiry.
    // Invalidates the stored token on failure to prevent any future reuse.
    private async Task<(User? user, Membership? membership, IActionResult? error)>
        RotateRefreshTokenAsync(string rawToken, CancellationToken ct)
    {
        var hashed = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        var user = await db.Users
            .Include(u => u.Memberships)
            .FirstOrDefaultAsync(u => u.RefreshTokenHash == hashed, ct);

        if (user is null || user.RefreshTokenExpiry < DateTime.UtcNow)
        {
            if (user is not null)
            {
                user.RefreshTokenHash = null;
                user.RefreshTokenExpiry = null;
                await db.SaveChangesAsync(ct);
            }
            return (null, null, Problem(title: "Unauthorized", detail: "Invalid or expired refresh token.", statusCode: 401));
        }

        if (user.Status == UserStatus.Suspended)
            return (null, null, Problem(title: "Forbidden", detail: "Account is suspended.", statusCode: 403));

        return (user, user.Memberships.FirstOrDefault(), null);
    }

    // Issues a new access + refresh token pair and persists the hashed refresh token.
    private async Task<(string accessToken, DateTime expiresAt, string rawRefresh)>
        IssueTokensAsync(User user, Membership? membership, CancellationToken ct)
    {
        var (accessToken, expiresAt) = jwt.GenerateAccessToken(user, membership);
        var (rawRefresh, hashedRefresh) = jwt.GenerateRefreshToken();

        user.RefreshTokenHash = hashedRefresh;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(RefreshTokenExpiryDays);
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return (accessToken, expiresAt, rawRefresh);
    }

    private void SetAuthCookies(string accessToken, DateTime expiresAt, string rawRefresh)
    {
        // Secure=false in development so localhost (HTTP) still works.
        var secure = !env.IsDevelopment();

        Response.Cookies.Append(AccessCookie, accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure   = secure,
            SameSite = SameSiteMode.Strict,
            Expires  = expiresAt,
            Path     = "/",
        });

        // Refresh cookie path is scoped to its own endpoint so the browser only
        // sends it when the portal explicitly calls /cookie/refresh.
        Response.Cookies.Append(RefreshCookie, rawRefresh, new CookieOptions
        {
            HttpOnly = true,
            Secure   = secure,
            SameSite = SameSiteMode.Strict,
            Expires  = DateTime.UtcNow.AddDays(RefreshTokenExpiryDays),
            Path     = RefreshCookiePath,
        });
    }

    private void ClearAuthCookies()
    {
        Response.Cookies.Delete(AccessCookie, new CookieOptions { Path = "/" });
        Response.Cookies.Delete(RefreshCookie, new CookieOptions { Path = RefreshCookiePath });
    }
}
