using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
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
    IPasswordHasherService hasher) : ControllerBase
{
    private const int RefreshTokenExpiryDays = 7;

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

        var (accessToken, expiresAt) = jwt.GenerateAccessToken(user, membership);
        var (rawRefresh, hashedRefresh) = jwt.GenerateRefreshToken();

        user.RefreshTokenHash = hashedRefresh;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(RefreshTokenExpiryDays);
        await db.SaveChangesAsync(ct);

        return Ok(new TokenResponse(accessToken, rawRefresh, expiresAt));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return Problem(title: "Validation", detail: "Email and password are required.", statusCode: 400);

        var email = req.Email.Trim().ToLowerInvariant();

        var user = await db.Users
            .Include(u => u.Memberships)
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        // Always call Verify — even for unknown users — to equalise response timing
        // and prevent email enumeration via response-time side channel.
        if (!hasher.Verify(user?.PasswordHash, req.Password))
            return Problem(title: "Unauthorized", detail: "Invalid email or password.", statusCode: 401);

        if (user!.Status == UserStatus.Suspended)
            return Problem(title: "Forbidden", detail: "Account is suspended.", statusCode: 403);

        var membership = user.Memberships.FirstOrDefault();
        var (accessToken, expiresAt) = jwt.GenerateAccessToken(user, membership);
        var (rawRefresh, hashedRefresh) = jwt.GenerateRefreshToken();

        user.RefreshTokenHash = hashedRefresh;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(RefreshTokenExpiryDays);
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new TokenResponse(accessToken, rawRefresh, expiresAt));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.RefreshToken))
            return Problem(title: "Validation", detail: "Refresh token is required.", statusCode: 400);

        var hashed = Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(req.RefreshToken)));

        var user = await db.Users
            .Include(u => u.Memberships)
            .FirstOrDefaultAsync(u => u.RefreshTokenHash == hashed, ct);

        // Invalidate the stored token on any failure to limit re-use window
        // after expiry (also protects against a stolen-but-expired token).
        if (user is null || user.RefreshTokenExpiry < DateTime.UtcNow)
        {
            if (user is not null)
            {
                user.RefreshTokenHash = null;
                user.RefreshTokenExpiry = null;
                await db.SaveChangesAsync(ct);
            }
            return Problem(title: "Unauthorized", detail: "Invalid or expired refresh token.", statusCode: 401);
        }

        if (user.Status == UserStatus.Suspended)
            return Problem(title: "Forbidden", detail: "Account is suspended.", statusCode: 403);

        var membership = user.Memberships.FirstOrDefault();
        var (accessToken, expiresAt) = jwt.GenerateAccessToken(user, membership);
        var (rawRefresh, hashedRefresh) = jwt.GenerateRefreshToken();

        // Rotate: old token is immediately replaced — reuse of the previous value returns 401.
        user.RefreshTokenHash = hashedRefresh;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(RefreshTokenExpiryDays);
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

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
}
