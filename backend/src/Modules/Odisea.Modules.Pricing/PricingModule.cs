using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Odisea.Modules.Pricing.Features.Calculator;
using Odisea.Modules.Pricing.Infrastructure;
using Odisea.Modules.Pricing.PublicApi;

namespace Odisea.Modules.Pricing;

public static class PricingModule
{
    public static IServiceCollection AddPricingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PricingDbContext>(options => options
            .UseNpgsql(
                configuration.GetConnectionString("Default"),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", PricingDbContext.Schema))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IPriceCalculator, PriceCalculator>();

        return services;
    }
}
