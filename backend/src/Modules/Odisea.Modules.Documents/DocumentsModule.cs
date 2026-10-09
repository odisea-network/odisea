using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Odisea.Modules.Documents.Features.Issuing;
using Odisea.Modules.Documents.Features.Numbering;
using Odisea.Modules.Documents.Features.Pdf;
using Odisea.Modules.Documents.Infrastructure;

namespace Odisea.Modules.Documents;

public static class DocumentsModule
{
    public static IServiceCollection AddDocumentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DocumentsDbContext>(options => options
            .UseNpgsql(
                configuration.GetConnectionString("Default"),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", DocumentsDbContext.Schema))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<NumberSeries>();
        services.AddScoped<DocumentService>();
        services.AddSingleton<DocumentPdfRenderer>();

        return services;
    }
}
