using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Odisea.Modules.Pricing;

public static class PricingModule
{
    public static IServiceCollection AddPricingModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Module registrations (DbContext, feature services) land here as features arrive.
        return services;
    }
}
