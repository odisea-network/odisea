using Odisea.Application.Common.Interfaces;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;

namespace Odisea.Application.Suppliers.Connectors;

public sealed class ImportRunner(IAppDbContext db, IConnectorRegistry connectors) : IImportRunner
{
    public async Task<ImportJob> RunAsync(SupplierConnection connection, CancellationToken ct)
    {
        var job = new ImportJob
        {
            SupplierConnectionId = connection.Id,
            Status = ImportJobStatus.Running,
            StartedAt = DateTime.UtcNow,
        };
        db.ImportJobs.Add(job);
        await db.SaveChangesAsync(ct);

        ConnectorRunResult result;
        try
        {
            var connector = connectors.For(connection.Kind);
            result = await connector.RunAsync(connection, ct);
        }
        catch (Exception ex)
        {
            // A throwing connector is itself a failed run, not an unhandled 500.
            result = ConnectorRunResult.Empty($"Connector threw: {ex.Message}");
        }

        job.OffersFetched = result.OffersFetched;
        job.OffersImported = result.OffersImported;
        job.OffersDeactivated = result.OffersDeactivated;
        job.Errors = string.Join('\n', result.Errors);
        job.Status = result.Succeeded ? ImportJobStatus.Succeeded : ImportJobStatus.Failed;
        job.CompletedAt = result.RanAt;
        job.UpdatedAt = result.RanAt;

        // Only a successful run advances freshness; a failed run leaves the last
        // good sync time intact so the dashboard shows real staleness.
        if (result.Succeeded)
        {
            connection.LastSyncedAt = result.RanAt;
            connection.UpdatedAt = result.RanAt;
        }

        await db.SaveChangesAsync(ct);
        return job;
    }
}
