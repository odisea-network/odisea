using Odisea.Domain.Common;
using Odisea.Domain.Enums;

namespace Odisea.Domain.Entities;

// One row per connector run. Persists what ConnectorRunResult reported so the
// supplier-health dashboard can show last-sync time, throughput and an error
// trend — and so a failed run's errors survive as a dead-letter record instead
// of vanishing into a log file.
public class ImportJob : Entity
{
    public Guid SupplierConnectionId { get; set; }
    public SupplierConnection? SupplierConnection { get; set; }

    public ImportJobStatus Status { get; set; } = ImportJobStatus.Running;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public int OffersFetched { get; set; }
    public int OffersImported { get; set; }
    public int OffersDeactivated { get; set; }

    // Dead-letter payload: the run's validation/parse errors, newline-joined.
    // Empty when the run succeeded. Kept as text (not a child table) because it's
    // only ever read whole, for display.
    public string Errors { get; set; } = string.Empty;
}
