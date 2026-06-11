using Odisea.Application.Suppliers.Connectors;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;

namespace Odisea.Infrastructure.Suppliers.Connectors;

// Manual connections don't fetch anything — operators enter offers through the
// portal UI. Running this connector is a no-op write that just stamps
// LastSyncedAt so the supplier-health dashboard can distinguish a connection
// that's recently been touched from a dormant one.
public sealed class ManualConnector : IConnector
{
    public SupplierConnectionKind Kind => SupplierConnectionKind.Manual;

    public Task<ConnectorRunResult> RunAsync(SupplierConnection connection, CancellationToken ct) =>
        Task.FromResult(ConnectorRunResult.Empty());
}
