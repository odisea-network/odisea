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
        // New adapters (XML, JSON API, CSV/SFTP) ship in follow-up PRs.
        services.AddSingleton<IConnector, ManualConnector>();
        services.AddSingleton<IConnectorRegistry, ConnectorRegistry>();

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
}
