using Odisea.Modules.Agencies;
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
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.UseSerilogRequestLogging();

    app.UseSwagger();
    app.UseSwaggerUI();

    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
