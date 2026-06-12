using Microsoft.Extensions.DependencyInjection;
using Odisea.Application.Suppliers.Connectors;
using Odisea.Application.Suppliers.Freshness;

namespace Odisea.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Scoped: both depend on the scoped IAppDbContext.
        services.AddScoped<IImportRunner, ImportRunner>();
        services.AddScoped<IFreshnessService, FreshnessService>();

        return services;
    }
}
