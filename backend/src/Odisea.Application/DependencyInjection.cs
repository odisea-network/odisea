using Microsoft.Extensions.DependencyInjection;
using Odisea.Application.Suppliers.Connectors;

namespace Odisea.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Scoped: depends on the scoped IAppDbContext.
        services.AddScoped<IImportRunner, ImportRunner>();

        return services;
    }
}
