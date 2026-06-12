using Odisea.Domain.Common;
using Odisea.Domain.Enums;

namespace Odisea.Domain.Entities;

// The raw record a connector saw for one offer, kept for traceability and
// deduplication. One row per (connection, external id) — the connector upserts
// it on every run, advancing LastSeenAt and the state. The normalized Offer.Source
// value object points back here conceptually via (SupplierConnectionId, ExternalId);
// this entity preserves the untransformed payload that produced it so we can
// diff, re-normalize, or debug a bad import without re-fetching from the supplier.
public class SourceOffer : Entity
{
    public Guid SupplierConnectionId { get; set; }
    public SupplierConnection? SupplierConnection { get; set; }

    // The supplier's own identifier for this offer — stable across runs.
    public string ExternalId { get; set; } = string.Empty;

    // Untransformed payload exactly as fetched (XML fragment, JSON object, CSV row).
    public string RawPayload { get; set; } = "{}";

    public ImportState State { get; set; } = ImportState.Pending;

    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}
