using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Odisea.Api;
using Odisea.Modules.Agencies;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.Infrastructure;
using Odisea.Modules.Agencies.Infrastructure.Data;
using Odisea.Modules.Booking;
using Odisea.Modules.Catalog;
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

    builder.Services.AddControllers()
        .AddApplicationPart(typeof(AgenciesModule).Assembly)
        .AddApplicationPart(typeof(CatalogModule).Assembly)
        .AddApplicationPart(typeof(PricingModule).Assembly)
        .AddApplicationPart(typeof(BookingModule).Assembly)
        .AddApplicationPart(typeof(IntegrationsModule).Assembly);

    builder.Services
        .AddAgenciesModule(builder.Configuration)
        .AddCatalogModule(builder.Configuration)
        .AddPricingModule(builder.Configuration)
        .AddBookingModule(builder.Configuration)
        .AddIntegrationsModule(builder.Configuration);

    // Brute-force protection on credential endpoints: 10 attempts/min per IP.
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
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

    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseCors();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    using (var scope = app.Services.CreateScope())
    {
        await scope.ServiceProvider.GetRequiredService<Odisea.Modules.Integrations.Infrastructure.IntegrationsDbContext>()
            .Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<Odisea.Modules.Catalog.Infrastructure.CatalogDbContext>()
            .Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<Odisea.Modules.Pricing.Infrastructure.PricingDbContext>()
            .Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<Odisea.Modules.Booking.Infrastructure.BookingDbContext>()
            .Database.MigrateAsync();
        var db = scope.ServiceProvider.GetRequiredService<AgenciesDbContext>();
        await db.Database.MigrateAsync();
        await AgenciesSeeder.SeedAsync(
            db,
            scope.ServiceProvider.GetRequiredService<IPasswordHasher<UserAccount>>(),
            app.Configuration,
            app.Logger);
        await DevDemoSeeder.SeedAsync(scope.ServiceProvider, app.Configuration, app.Logger);
    }

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
