using Odisea.Application.Suppliers.Connectors;
using Odisea.Domain.Entities;

namespace Odisea.Application.Suppliers.Dtos;

public record SupplierConnectionDto(
    Guid Id,
    Guid OperatorId,
    string Kind,
    string Name,
    string Status,
    DateTime? LastSyncedAt,
    DateTime CreatedAt
);

// Returned by POST /api/v1/supplier-connections/{id}/run.
public record ConnectorRunResultDto(
    bool Succeeded,
    int OffersFetched,
    int OffersImported,
    int OffersDeactivated,
    IReadOnlyList<string> Errors,
    DateTime RanAt
);

public static class SupplierConnectionMappings
{
    public static SupplierConnectionDto ToDto(this SupplierConnection c) => new(
        c.Id, c.OperatorId, c.Kind.ToString(), c.Name,
        c.Status.ToString(), c.LastSyncedAt, c.CreatedAt);

    public static ConnectorRunResultDto ToDto(this ConnectorRunResult r) => new(
        r.Succeeded, r.OffersFetched, r.OffersImported, r.OffersDeactivated,
        r.Errors, r.RanAt);
}
