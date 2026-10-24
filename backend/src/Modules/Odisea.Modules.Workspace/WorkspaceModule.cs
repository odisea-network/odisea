using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Odisea.Modules.Workspace.Features.ExternalReservations;
using Odisea.Modules.Workspace.Features.Inquiries;
using Odisea.Modules.Workspace.Features.Usage;
using Odisea.Modules.Workspace.Infrastructure;

namespace Odisea.Modules.Workspace;

public static class WorkspaceModule
{
    /// Public (anonymous) workspace endpoints are called from agency websites: any origin, no credentials.
    public const string PublicCorsPolicy = "WorkspacePublic";

    /// Per-IP budget for the anonymous endpoints (tracking page, later form submissions).
    public const string PublicRateLimitPolicy = "workspace-public";

    public static IServiceCollection AddWorkspaceModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<WorkspaceDbContext>(options => options
            .UseNpgsql(
                configuration.GetConnectionString("Default"),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", WorkspaceDbContext.Schema))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<ExternalReservationService>();
        services.AddScoped<InquiryFormService>();
        services.AddScoped<WorkspaceUsageService>();
        return services;
    }
}
