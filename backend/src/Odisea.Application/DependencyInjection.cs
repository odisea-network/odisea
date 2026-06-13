using Microsoft.Extensions.DependencyInjection;
using Odisea.Application.Catalog.Access;
using Odisea.Application.Suppliers.Connectors;
using Odisea.Application.Suppliers.Freshness;

namespace Odisea.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Scoped: all depend on the scoped IAppDbContext.
        services.AddScoped<IImportRunner, ImportRunner>();
        services.AddScoped<IFreshnessService, FreshnessService>();
        services.AddScoped<IOfferAccessPolicy, OfferAccessPolicy>();

        return services;
    }
}
