using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Odisea.Application.Common.Interfaces;
using Odisea.Application.Suppliers.Connectors;
using Odisea.Application.Webhooks;
using Odisea.Infrastructure.Data;
using Odisea.Infrastructure.Services;
using Odisea.Infrastructure.Suppliers.Connectors;
using Odisea.Infrastructure.Webhooks;

namespace Odisea.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Default")
            ?? "Host=localhost;Port=5432;Database=odisea;Username=odisea;Password=odisea";

        services.AddDbContext<AppDbContext>(opt =>
            opt.UseNpgsql(connectionString)
               .UseSnakeCaseNamingConvention());

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<IJwtService, JwtService>();
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();

        // Connector platform. Each adapter registers under IConnector so the
        // registry can fan them out by SupplierConnectionKind at run time.
        // Manual is stateless (singleton); JsonApi needs a scoped DbContext + a
        // typed HttpClient, so the registry is scoped to compose them per request.
        // New adapters (XML, CSV/SFTP) ship in follow-up PRs.
        services.AddSingleton<IConnector, ManualConnector>();
        services.AddHttpClient<JsonApiConnector>(c => c.Timeout = TimeSpan.FromSeconds(15));
        services.AddScoped<IConnector>(sp => sp.GetRequiredService<JsonApiConnector>());
        services.AddScoped<IConnectorRegistry, ConnectorRegistry>();

        // Background scheduler: periodically syncs stale connections + sweeps
        // freshness. Off unless enabled in config, so test hosts stay quiet.
        var schedulerOptions = ReadSchedulerOptions(config);
        services.AddSingleton(schedulerOptions);
        if (schedulerOptions.Enabled)
            services.AddHostedService<ConnectorSchedulerService>();

        // Outbound webhook delivery — typed HttpClient with a tight timeout so a
        // slow receiver can't hang the request that triggered the event.
        services.AddHttpClient<IWebhookSender, HttpWebhookSender>(c =>
            c.Timeout = TimeSpan.FromSeconds(5));

        // JWT secret is validated here at startup; the token validation middleware is
        // wired in Program.cs because JwtBearerDefaults lives in the Web SDK.
        var jwtSecret = config["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret must be set in configuration.");

        if (jwtSecret.Length < 32)
            throw new InvalidOperationException("Jwt:Secret must be at least 32 characters.");

        return services;
    }

    // Read via the indexer (not the binder) to keep Infrastructure off the
    // Configuration.Binder package. Missing keys fall back to the option defaults.
    private static ConnectorSchedulerOptions ReadSchedulerOptions(IConfiguration config)
    {
        var section = config.GetSection(ConnectorSchedulerOptions.SectionName);
        var options = new ConnectorSchedulerOptions();

        if (bool.TryParse(section["Enabled"], out var enabled))
            options.Enabled = enabled;
        if (int.TryParse(section["PollIntervalSeconds"], out var poll) && poll > 0)
            options.PollInterval = TimeSpan.FromSeconds(poll);
        if (int.TryParse(section["SyncIntervalMinutes"], out var sync) && sync > 0)
            options.SyncInterval = TimeSpan.FromMinutes(sync);

        return options;
    }
}
