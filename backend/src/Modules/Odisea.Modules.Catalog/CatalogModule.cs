using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Odisea.Modules.Catalog;

public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Module registrations (DbContext, feature services) land here as features arrive.
        return services;
    }
}
