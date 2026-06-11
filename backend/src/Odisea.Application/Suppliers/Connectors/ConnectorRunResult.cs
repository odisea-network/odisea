namespace Odisea.Application.Suppliers.Connectors;

// Outcome of a single connector run. Counts feed the supplier-health dashboard;
// Errors feeds the dead-letter UI. RanAt is what gets written back to
// SupplierConnection.LastSyncedAt so freshness queries (#12) see fresh data.
public sealed record ConnectorRunResult(
    int OffersFetched,
    int OffersImported,
    int OffersDeactivated,
    IReadOnlyList<string> Errors,
    DateTime RanAt)
{
    public bool Succeeded => Errors.Count == 0;

    public static ConnectorRunResult Empty(string? reason = null) =>
        new(0, 0, 0,
            reason is null ? [] : [reason],
            DateTime.UtcNow);
}
