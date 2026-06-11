using Odisea.Domain.Enums;

namespace Odisea.Application.Suppliers.Connectors;

// Looks up the IConnector for a given SupplierConnectionKind. Centralised so the
// WebAPI layer doesn't reach into IServiceProvider for a typed connector lookup.
public interface IConnectorRegistry
{
    IConnector For(SupplierConnectionKind kind);
}
