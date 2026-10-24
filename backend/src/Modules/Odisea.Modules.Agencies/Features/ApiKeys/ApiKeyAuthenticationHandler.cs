using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Odisea.Modules.Agencies.PublicApi;

namespace Odisea.Modules.Agencies.Features.ApiKeys;

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions;

/// Turns a valid X-Api-Key into the same identity an agency login produces (user_type=agency,
/// agency_id), so every agency-scoped endpoint and policy works unchanged. The subject is the
/// key id and the role is ApiClient, which never satisfies an operator policy.
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ApiKeyService keys)
    : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationDefaults.HeaderName, out var values))
            return AuthenticateResult.NoResult();

        var presented = values.ToString().Trim();
        var principal = presented.Length == 0
            ? null
            : await keys.AuthenticateAsync(presented, Context.RequestAborted);

        if (principal is null)
        {
            // The prefix is the operator-visible identifier, never the secret. Anything that is
            // not shaped like one of our keys (a pasted password, say) is not echoed at all.
            var looksLikeOurs = presented.Length == ApiKeyService.KeyLength
                && presented.StartsWith(ApiKeyAuthenticationDefaults.KeyPrefixMarker, StringComparison.Ordinal);
            Logger.LogWarning("Rejected API key {Prefix} from {RemoteIp}",
                looksLikeOurs ? presented[..16] : "(malformed)",
                Context.Connection.RemoteIpAddress);
            return AuthenticateResult.Fail("Invalid API key");
        }

        var identity = new ClaimsIdentity(
            [
                new Claim("sub", principal.KeyId.ToString()),
                new Claim("name", principal.Name),
                new Claim(AuthClaims.UserType, AuthClaims.UserTypeAgency),
                new Claim(AuthClaims.AgencyId, principal.AgencyId.ToString()),
                new Claim(AuthClaims.Role, AuthClaims.RoleApiClient),
                new Claim(AuthClaims.AuthMethod, AuthClaims.AuthMethodApiKey),
            ],
            Scheme.Name,
            nameType: "name",
            roleType: AuthClaims.Role);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = $"{ApiKeyAuthenticationDefaults.Scheme} realm=\"odisea\"";
        return Task.CompletedTask;
    }
}
