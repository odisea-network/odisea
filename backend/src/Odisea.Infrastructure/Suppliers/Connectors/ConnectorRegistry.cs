using Odisea.Application.Suppliers.Connectors;
using Odisea.Domain.Enums;

namespace Odisea.Infrastructure.Suppliers.Connectors;

public sealed class ConnectorRegistry : IConnectorRegistry
{
    private readonly Dictionary<SupplierConnectionKind, IConnector> _byKind;

    public ConnectorRegistry(IEnumerable<IConnector> connectors)
    {
        _byKind = connectors.ToDictionary(c => c.Kind);
    }

    public IConnector For(SupplierConnectionKind kind)
    {
        if (_byKind.TryGetValue(kind, out var connector))
            return connector;

        throw new NotSupportedException(
            $"No connector registered for SupplierConnectionKind={kind}. " +
            $"Implement IConnector and register it via AddInfrastructureServices.");
    }
}
