using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Odisea.Modules.Agencies;

public static class AgenciesModule
{
    public static IServiceCollection AddAgenciesModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Module registrations (DbContext, feature services) land here as features arrive.
        return services;
    }
}
