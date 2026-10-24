using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using Odisea.Api;
using Odisea.Modules.Agencies;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Booking;
using Odisea.Modules.Catalog;
using Odisea.Modules.Documents;
using Odisea.Modules.Integrations;
using Odisea.Modules.Pricing;
using Odisea.SharedKernel;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, config) =>
        config.ReadFrom.Configuration(context.Configuration));

    builder.Services.AddSingleton<IClock, SystemClock>();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();
    builder.Services.AddProblemDetails();

    // Migrate every module schema + seed before the server listens (also under WebApplicationFactory).
    builder.Services.AddHostedService<StartupMigrator>();

    builder.Services.AddControllers()
        // Enums bind and render by name ("Passport"), which is what external API clients expect.
        .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
        .AddApplicationPart(typeof(AgenciesModule).Assembly)
        .AddApplicationPart(typeof(CatalogModule).Assembly)
        .AddApplicationPart(typeof(PricingModule).Assembly)
        .AddApplicationPart(typeof(BookingModule).Assembly)
        .AddApplicationPart(typeof(IntegrationsModule).Assembly)
        .AddApplicationPart(typeof(DocumentsModule).Assembly);

    builder.Services
        .AddAgenciesModule(builder.Configuration)
        .AddCatalogModule(builder.Configuration)
        .AddPricingModule(builder.Configuration)
        .AddBookingModule(builder.Configuration)
        .AddIntegrationsModule(builder.Configuration)
        .AddDocumentsModule(builder.Configuration);

    // Brute-force protection on credential endpoints: 10 attempts/min per IP, and a per-key
    // budget for external API clients. The limiter runs before authentication, so the key
    // partition is a hash of the presented header (never the raw key); unknown keys are
    // throttled too, which also slows down key guessing. Portal (JWT) traffic is unlimited here.
    var apiKeyPerMinute = builder.Configuration.GetValue("RateLimiting:ApiKeyPerMinute", 60);
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            context.Request.Headers.TryGetValue(ApiKeyAuthenticationDefaults.HeaderName, out var presentedKey)
                ? RateLimitPartition.GetFixedWindowLimiter(
                    "apikey:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(presentedKey.ToString()))),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = apiKeyPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    })
                : RateLimitPartition.GetNoLimiter("none"));
        options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    });

    // Strict allowlist; empty config means no cross-origin access at all.
    var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
        policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
        });
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer",
                    },
                },
                []
            },
        });
        // A second requirement object means "either": external agencies use a key, portal users a token.
        options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
        {
            Name = ApiKeyAuthenticationDefaults.HeaderName,
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Description = "Per-agency API key for external partners (odk_live_…)",
        });
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "ApiKey",
                    },
                },
                []
            },
        });
    });

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseStatusCodePages();

    if (!app.Environment.IsDevelopment())
        app.UseHsts();

    app.Use(async (context, next) =>
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["X-Permitted-Cross-Domain-Policies"] = "none";
        await next();
    });

    app.UseSerilogRequestLogging();

    // API responses are authorized, per-user data — never cache them anywhere.
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
            context.Response.Headers.CacheControl = "no-store";
        await next();
    });

    // Swagger is a dev tool: on in Development, elsewhere only by explicit opt-in.
    if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception ex) when (ex is not Microsoft.Extensions.Hosting.HostAbortedException)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Exposes the entry-point type to WebApplicationFactory<Program> in Odisea.IntegrationTests.
public partial class Program;
