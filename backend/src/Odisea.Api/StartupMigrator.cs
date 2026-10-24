using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.Infrastructure;
using Odisea.Modules.Agencies.Infrastructure.Data;
using Odisea.Modules.Booking.Infrastructure;
using Odisea.Modules.Catalog.Infrastructure;
using Odisea.Modules.Documents.Infrastructure;
using Odisea.Modules.Integrations.Infrastructure;
using Odisea.Modules.Notifications.Infrastructure;
using Odisea.Modules.Pricing.Infrastructure;
using Odisea.Modules.Workspace.Infrastructure;

namespace Odisea.Api;

/// Applies every module's migrations and runs the seeders before the server starts
/// listening. A hosted service (rather than code after builder.Build()) so the same
/// path runs under WebApplicationFactory, which unwinds Program.cs at Build().
public sealed class StartupMigrator(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<StartupMigrator> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var services = scope.ServiceProvider;

        await services.GetRequiredService<IntegrationsDbContext>().Database.MigrateAsync(ct);
        await services.GetRequiredService<CatalogDbContext>().Database.MigrateAsync(ct);
        await services.GetRequiredService<PricingDbContext>().Database.MigrateAsync(ct);
        await services.GetRequiredService<BookingDbContext>().Database.MigrateAsync(ct);
        await services.GetRequiredService<DocumentsDbContext>().Database.MigrateAsync(ct);
        await services.GetRequiredService<NotificationsDbContext>().Database.MigrateAsync(ct);
        await services.GetRequiredService<WorkspaceDbContext>().Database.MigrateAsync(ct);

        // Agencies last: its seeder needs the operator table, and the demo seeder needs everything.
        var agencies = services.GetRequiredService<AgenciesDbContext>();
        await agencies.Database.MigrateAsync(ct);
        await AgenciesSeeder.SeedAsync(
            agencies,
            services.GetRequiredService<IPasswordHasher<UserAccount>>(),
            configuration,
            logger,
            ct);
        await DevDemoSeeder.SeedAsync(services, configuration, logger);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
