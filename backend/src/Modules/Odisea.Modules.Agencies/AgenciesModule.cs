using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.Features.Auth;
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

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

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
            options.AddPolicy(AuthPolicies.Agency,
                p => p.RequireClaim(AuthClaims.UserType, AuthClaims.UserTypeAgency));
        });

        return services;
    }
}
