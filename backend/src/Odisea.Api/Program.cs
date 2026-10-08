using Microsoft.AspNetCore.Identity;
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
    app.UseSerilogRequestLogging();

    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    using (var scope = app.Services.CreateScope())
    {
        await scope.ServiceProvider.GetRequiredService<Odisea.Modules.Integrations.Infrastructure.IntegrationsDbContext>()
            .Database.MigrateAsync();
        var db = scope.ServiceProvider.GetRequiredService<AgenciesDbContext>();
        await db.Database.MigrateAsync();
        await AgenciesSeeder.SeedAsync(
            db,
            scope.ServiceProvider.GetRequiredService<IPasswordHasher<UserAccount>>(),
            app.Configuration,
            app.Logger);
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
