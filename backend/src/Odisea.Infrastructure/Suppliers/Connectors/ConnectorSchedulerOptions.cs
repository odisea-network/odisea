namespace Odisea.Infrastructure.Suppliers.Connectors;

// Cadence for the background connector scheduler. Bound from the
// "ConnectorScheduler" configuration section in Infrastructure DI.
public sealed class ConnectorSchedulerOptions
{
    public const string SectionName = "ConnectorScheduler";

    // Master switch. Off by default so test hosts and one-off CLI runs don't spin
    // up a background importer; the API turns it on via configuration.
    public bool Enabled { get; set; }

    // How often the loop wakes to look for due connections.
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(5);

    // A connection is re-synced once its last successful sync is older than this.
    public TimeSpan SyncInterval { get; set; } = TimeSpan.FromHours(6);
}
