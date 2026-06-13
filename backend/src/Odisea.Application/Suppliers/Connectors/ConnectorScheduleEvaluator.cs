using Odisea.Domain.Entities;
using Odisea.Domain.Enums;

namespace Odisea.Application.Suppliers.Connectors;

// Decides which supplier connections are due for an automatic sync. Kept pure
// (entity + clock + interval in, bool out) so the cadence rules are unit-testable
// without spinning up the hosted service.
public static class ConnectorScheduleEvaluator
{
    // A connection is due when it's Active and either has never synced or its last
    // successful sync is older than the configured interval. Paused/Disabled
    // connections are never auto-run — an operator must re-activate them.
    public static bool IsDue(SupplierConnection connection, DateTime nowUtc, TimeSpan syncInterval)
    {
        if (connection.Status != SupplierConnectionStatus.Active)
            return false;

        return connection.LastSyncedAt is not { } last
            || nowUtc - last >= syncInterval;
    }

    public static IEnumerable<SupplierConnection> DueConnections(
        IEnumerable<SupplierConnection> connections, DateTime nowUtc, TimeSpan syncInterval) =>
        connections.Where(c => IsDue(c, nowUtc, syncInterval));
}
