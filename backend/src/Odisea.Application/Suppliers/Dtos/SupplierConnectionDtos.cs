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

public record ImportJobDto(
    Guid Id,
    Guid SupplierConnectionId,
    string Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int OffersFetched,
    int OffersImported,
    int OffersDeactivated,
    IReadOnlyList<string> Errors
);

// Per-connection rollup for the supplier-health dashboard.
public record ConnectionHealthDto(
    Guid SupplierConnectionId,
    string Name,
    string Kind,
    string Status,
    DateTime? LastSyncedAt,
    DateTime? LastSuccessfulRunAt,
    string? LastRunStatus,
    int RecentRuns,
    int RecentFailures
);

public static class SupplierConnectionMappings
{
    public static SupplierConnectionDto ToDto(this SupplierConnection c) => new(
        c.Id, c.OperatorId, c.Kind.ToString(), c.Name,
        c.Status.ToString(), c.LastSyncedAt, c.CreatedAt);

    public static ConnectorRunResultDto ToDto(this ConnectorRunResult r) => new(
        r.Succeeded, r.OffersFetched, r.OffersImported, r.OffersDeactivated,
        r.Errors, r.RanAt);

    public static ImportJobDto ToDto(this ImportJob j) => new(
        j.Id, j.SupplierConnectionId, j.Status.ToString(),
        j.StartedAt, j.CompletedAt, j.OffersFetched, j.OffersImported,
        j.OffersDeactivated, SplitErrors(j.Errors));

    // Errors persist as a newline-joined string; split back to a list for the DTO.
    private static IReadOnlyList<string> SplitErrors(string errors) =>
        string.IsNullOrEmpty(errors)
            ? []
            : errors.Split('\n', StringSplitOptions.RemoveEmptyEntries);
}
