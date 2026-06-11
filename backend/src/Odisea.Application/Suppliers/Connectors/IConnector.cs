using Odisea.Domain.Entities;
using Odisea.Domain.Enums;

namespace Odisea.Application.Suppliers.Connectors;

// One connector per SupplierConnectionKind. Every adapter (Manual, XML, JSON API,
// CSV/SFTP) implements this contract and is registered in IConnectorRegistry.
// The single RunAsync entry point encapsulates the strategy doc's six-step
// lifecycle (fetch → parse → validate → normalize → upsert → deactivate) so
// callers don't need to know which steps a specific kind actually has.
public interface IConnector
{
    SupplierConnectionKind Kind { get; }

    Task<ConnectorRunResult> RunAsync(SupplierConnection connection, CancellationToken ct);
}
