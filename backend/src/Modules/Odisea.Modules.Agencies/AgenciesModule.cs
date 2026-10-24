using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.Features.ApiKeys;
using Odisea.Modules.Agencies.Features.Auth;
using Odisea.Modules.Agencies.Features.Branding;
using Odisea.Modules.Agencies.Infrastructure;
using Odisea.Modules.Agencies.PublicApi;

namespace Odisea.Modules.Agencies;

public static class AgenciesModule
{
    public static IServiceCollection AddAgenciesModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                o => Encoding.UTF8.GetByteCount(o.Secret) >= 32,
                "Jwt:Secret must be at least 32 bytes (256 bits). Set it via user-secrets or environment.")
            .Validate(
                o => o.AccessTokenMinutes is > 0 and <= 60,
                "Jwt:AccessTokenMinutes must be between 1 and 60.")
            .ValidateOnStart();

        services.AddDbContext<AgenciesDbContext>(options => options
            .UseNpgsql(
                configuration.GetConnectionString("Default"),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", AgenciesDbContext.Schema))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
        services.AddScoped<TokenService>();
        services.AddScoped<RefreshTokenService>();
        services.AddScoped<AuthService>();
        services.AddScoped<IAgencyLookup, Features.Lookup.AgencyLookup>();
        services.AddScoped<IAgencyEntitlements, Features.Plans.AgencyEntitlementsService>();

        // Security plumbing lives here. The key ring is persisted in agencies.data_protection_keys
        // so protected payloads (offer tokens, encrypted passenger documents) survive restarts
        // and are shared across instances. Keys are not encrypted at rest yet — add
        // ProtectKeysWithCertificate before production. Other modules consume
        // IDataProtectionProvider as a framework service.
        services.AddDataProtection()
            .SetApplicationName("Odisea")
            .PersistKeysToDbContext<AgenciesDbContext>();

        services.AddScoped<ApiKeyService>();
        services.AddScoped<BrandingService>();

        // Two ways in, one identity shape. The default scheme is a selector: a request carrying
        // X-Api-Key is authenticated by the API-key handler (external agencies), anything else
        // by JWT bearer (portal users). Either way HttpContext.User ends up with user_type and
        // agency_id, so CurrentUser and every policy work unchanged. Header present → key wins.
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = ApiKeyAuthenticationDefaults.SelectorScheme;
                options.DefaultChallengeScheme = ApiKeyAuthenticationDefaults.SelectorScheme;
            })
            .AddPolicyScheme(ApiKeyAuthenticationDefaults.SelectorScheme, "JWT bearer or API key", options =>
                options.ForwardDefaultSelector = context =>
                    context.Request.Headers.ContainsKey(ApiKeyAuthenticationDefaults.HeaderName)
                        ? ApiKeyAuthenticationDefaults.Scheme
                        : JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer()
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationDefaults.Scheme, null);

        // Bound lazily from the validated JwtOptions rather than read from IConfiguration here:
        // configuration applied after registration (e.g. WebApplicationFactory overrides) must
        // reach the bearer handler too, and the secret is validated once, on start.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    RoleClaimType = AuthClaims.Role,
                    NameClaimType = "name",
                };
            });

        services.AddAuthorization(options =>
        {
            // Secure by default: every endpoint requires an authenticated caller
            // unless it explicitly opts out with [AllowAnonymous].
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            options.AddPolicy(AuthPolicies.Operator,
                p => p.RequireClaim(AuthClaims.UserType, AuthClaims.UserTypeOperator));
            options.AddPolicy(AuthPolicies.OperatorAdmin,
                p => p.RequireClaim(AuthClaims.UserType, AuthClaims.UserTypeOperator)
                      .RequireClaim(AuthClaims.Role, AuthClaims.RoleOperatorAdmin));
            options.AddPolicy(AuthPolicies.Agency,
                p => p.RequireClaim(AuthClaims.UserType, AuthClaims.UserTypeAgency));
        });

        return services;
    }
}
