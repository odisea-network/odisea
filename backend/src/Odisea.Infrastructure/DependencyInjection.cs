using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Odisea.Application.Common.Interfaces;
using Odisea.Infrastructure.Data;
using Odisea.Infrastructure.Services;

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

        // JWT secret is validated here at startup; the token validation middleware is
        // wired in Program.cs because JwtBearerDefaults lives in the Web SDK.
        var jwtSecret = config["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret must be set in configuration.");

        if (jwtSecret.Length < 32)
            throw new InvalidOperationException("Jwt:Secret must be at least 32 characters.");

        return services;
    }
}
