using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Odisea.Modules.Integrations.Adapters.Mock;
using Odisea.Modules.Integrations.Features;
using Odisea.Modules.Integrations.Infrastructure;
using Odisea.Modules.Integrations.PublicApi;

namespace Odisea.Modules.Integrations;

public static class IntegrationsModule
{
    public static IServiceCollection AddIntegrationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IntegrationsDbContext>(options => options
            .UseNpgsql(
                configuration.GetConnectionString("Default"),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", IntegrationsDbContext.Schema))
            .UseSnakeCaseNamingConvention());

        services.Configure<MockProviderOptions>(configuration.GetSection(MockProviderOptions.SectionName));

        // Every adapter registers as IReservationProvider; the registry indexes them
        // by code and hands out logging-wrapped instances.
        services.AddScoped<IReservationProvider, MockReservationProvider>();
        services.AddScoped<IProviderRegistry, ProviderRegistry>();

        return services;
    }
}
