using Odisea.Domain.Enums;

namespace Odisea.Domain.ValueObjects;

// Supply-side lineage for an Offer: which connection brought it in, the supplier's
// own ID for it, and the freshness/state of the last import. Owned by Offer and
// persisted inline (flattened into source_* columns). Null for agency-private or
// hand-seeded offers that have no upstream source.
public class OfferSource
{
    public Guid SupplierConnectionId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public DateTime? LastImportedAt { get; set; }
    public ImportState ImportState { get; set; } = ImportState.Pending;
}
