using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Odisea.Application.Common.Interfaces;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;

namespace Odisea.WebAPI.Auth;

/// <summary>
/// Authenticates API/embed consumers presenting <c>Authorization: ApiKey od_…</c>.
/// Returns NoResult for any other scheme so JWT bearer can still handle it — the
/// header's scheme token, not precedence rules, decides which handler succeeds.
/// </summary>
public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IAppDbContext db) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string ScopeClaimType = "scope";

    private const string Prefix = "ApiKey ";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var header))
            return AuthenticateResult.NoResult();

        var raw = header.ToString();
        if (!raw.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var rawKey = raw[Prefix.Length..].Trim();
        if (string.IsNullOrEmpty(rawKey))
            return AuthenticateResult.Fail("Empty API key.");

        var hash = ApiKey.Hash(rawKey);
        var key = await db.ApiKeys.AsNoTracking()
            .FirstOrDefaultAsync(k => k.KeyHash == hash, Context.RequestAborted);

        if (key is null)
            return AuthenticateResult.Fail("Unknown API key.");

        if (!key.IsActive(DateTime.UtcNow))
            return AuthenticateResult.Fail("API key is revoked or expired.");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, key.Id.ToString()),
            new("tenantType", TenantType.Agency.ToString()),
            new("tenantId", key.AgencyId.ToString()),
        };
        claims.AddRange(key.ScopeList.Select(s => new Claim(ScopeClaimType, s)));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}
