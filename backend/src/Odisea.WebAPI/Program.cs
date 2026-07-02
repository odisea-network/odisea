using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Odisea.Application;
using Odisea.Application.Common.Interfaces;
using Odisea.Domain.Common;
using Odisea.Domain.Enums;
using Odisea.Infrastructure;
using Odisea.Infrastructure.Data;
using Odisea.WebAPI.Auth;
using Odisea.WebAPI.Middleware;
using Odisea.WebAPI.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, _, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// JWT bearer token validation — lives here because JwtBearerDefaults is Web-SDK-only.
var jwtSecret = builder.Configuration["Jwt:Secret"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "odisea-api",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "odisea-client",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        };

        // Allow the Angular portal to authenticate via HttpOnly cookie without
        // exposing the token to JavaScript.  The Authorization header takes
        // precedence: if both are present the header wins (API consumers).
        opts.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (!ctx.Request.Headers.ContainsKey("Authorization") &&
                    ctx.Request.Cookies.TryGetValue("od_at", out var token))
                {
                    ctx.Token = token;
                }
                return Task.CompletedTask;
            },
        };
    })
    // Embed/API consumers authenticate with `Authorization: ApiKey od_…`. The
    // handler returns NoResult for non-ApiKey schemes, so JWT bearer still works.
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName, _ => { });

// Authorization policies — PlatformAdmin satisfies every lower-tier policy.
builder.Services.AddAuthorization(opts =>
{
    // Scope-gated policies for API-key consumers; these resolve only against the
    // ApiKey scheme so a JWT cannot satisfy an embed scope and vice-versa.
    opts.AddPolicy("EmbedPublicationsRead", p =>
    {
        p.AuthenticationSchemes = [ApiKeyAuthenticationHandler.SchemeName];
        p.RequireClaim(ApiKeyAuthenticationHandler.ScopeClaimType, ApiKeyScopes.PublicationsRead);
    });

    opts.AddPolicy("EmbedEventsWrite", p =>
    {
        p.AuthenticationSchemes = [ApiKeyAuthenticationHandler.SchemeName];
        p.RequireClaim(ApiKeyAuthenticationHandler.ScopeClaimType, ApiKeyScopes.EventsWrite);
    });

    // EmbedRead — the public read path (manifest + resolved offers). Satisfied by
    // EITHER an embed key with publications:read (the anonymous-site embed) OR a
    // portal agency role over JWT (the builder preview hits the same offers URL).
    // Both schemes run so whichever credential is present populates the principal;
    // per-agency ownership is enforced in the actions via IAgencyContext.
    opts.AddPolicy("EmbedRead", p =>
    {
        p.AuthenticationSchemes =
            [ApiKeyAuthenticationHandler.SchemeName, JwtBearerDefaults.AuthenticationScheme];
        p.RequireAssertion(ctx =>
            ctx.User.HasClaim(ApiKeyAuthenticationHandler.ScopeClaimType, ApiKeyScopes.PublicationsRead) ||
            ctx.User.IsInRole(UserRole.PlatformAdmin.ToString()) ||
            ctx.User.IsInRole(UserRole.AgencyAdmin.ToString()) ||
            ctx.User.IsInRole(UserRole.AgencyEditor.ToString()));
    });

    opts.AddPolicy("PlatformAdmin", p =>
        p.RequireRole(UserRole.PlatformAdmin.ToString()));

    opts.AddPolicy("OperatorAdmin", p =>
        p.RequireRole(
            UserRole.PlatformAdmin.ToString(),
            UserRole.OperatorAdmin.ToString()));

    opts.AddPolicy("AgencyAdmin", p =>
        p.RequireRole(
            UserRole.PlatformAdmin.ToString(),
            UserRole.AgencyAdmin.ToString()));

    opts.AddPolicy("AgencyMember", p =>
        p.RequireRole(
            UserRole.PlatformAdmin.ToString(),
            UserRole.AgencyAdmin.ToString(),
            UserRole.AgencyEditor.ToString()));
});

// IHttpContextAccessor is needed by RequestContext to read claims.
builder.Services.AddHttpContextAccessor();

// Single scoped service fulfils all three context interfaces.
builder.Services.AddScoped<RequestContext>();
builder.Services.AddScoped<IUserContext>(sp => sp.GetRequiredService<RequestContext>());
builder.Services.AddScoped<IAgencyContext>(sp => sp.GetRequiredService<RequestContext>());
builder.Services.AddScoped<IOperatorContext>(sp => sp.GetRequiredService<RequestContext>());

// Rate limiting — the anonymous analytics ingest endpoint ([EnableRateLimiting("events")])
// is throttled per client IP. Tighter API-key-based limits arrive with #27.
builder.Services.AddRateLimiter(opts =>
{
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opts.AddPolicy("events", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Odisea Network API", Version = "v1" });

    // Allow Swagger UI to send the Bearer token for protected endpoints.
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT access token.",
    };
    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

const string PublicEmbedCors = "PublicEmbedCors";
const string PortalCors      = "PortalCors";

var portalOrigins = builder.Configuration
    .GetSection("Cors:PortalOrigins")
    .Get<string[]>() ?? ["http://localhost:4200"];

builder.Services.AddCors(o =>
{
    // PublicEmbedCors — read-only, any origin, NO credentials. Applied only to the
    // public embed endpoints via [EnableCors] so a browser can fetch them cross-origin.
    // Per-publication origin enforcement is done by EmbedSecurityMiddleware, not CORS
    // (the allowlist is dynamic, so it can't be expressed as a static CORS policy).
    o.AddPolicy(PublicEmbedCors, p =>
        p.AllowAnyOrigin()
         .AllowAnyHeader()
         .WithMethods("GET", "HEAD", "OPTIONS"));

    // PortalCors — specific origins + credentials, applied only to the
    // /auth/cookie/* endpoints via [EnableCors("PortalCors")].
    o.AddPolicy(PortalCors, p =>
        p.WithOrigins(portalOrigins)
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials());
});

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Odisea API v1"));
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRateLimiter();

// CORS middleware applies the per-endpoint [EnableCors] policies (PublicEmbedCors,
// PortalCors). No default policy — endpoints with no [EnableCors] are same-origin only.
app.UseCors();

// Enforce per-Publication origin allowlists on the public embed endpoints.
app.UseMiddleware<EmbedSecurityMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await Seeder.SeedAsync(db);
}

app.Run();
