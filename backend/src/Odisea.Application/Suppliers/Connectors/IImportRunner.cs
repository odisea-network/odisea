using Odisea.Domain.Entities;

namespace Odisea.Application.Suppliers.Connectors;

// Orchestrates one connector run end to end: opens an ImportJob, dispatches the
// connector, records the outcome (job status + counts + dead-letter errors), and
// stamps the connection's freshness. The controller calls this instead of the
// registry directly so every run leaves an auditable ImportJob behind.
public interface IImportRunner
{
    Task<ImportJob> RunAsync(SupplierConnection connection, CancellationToken ct);
}
