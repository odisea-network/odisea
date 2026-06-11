using Odisea.Domain.Common;
using Odisea.Domain.Enums;

namespace Odisea.Domain.Entities;

// A connection between a supplier (Operator) and our import pipeline. Captures how
// offers flow in (Kind), provider-specific settings (ConfigJson), and sync health.
public class SupplierConnection : Entity
{
    public Guid OperatorId { get; set; }
    public Operator? Operator { get; set; }

    public SupplierConnectionKind Kind { get; set; }
    public string Name { get; set; } = string.Empty;

    // Provider-specific settings (endpoint, credentials ref, field mappings). Shape
    // varies by Kind, so it stays opaque JSON until connectors land in Phase 2.
    public string ConfigJson { get; set; } = "{}";

    public DateTime? LastSyncedAt { get; set; }
    public SupplierConnectionStatus Status { get; set; } = SupplierConnectionStatus.Active;
}
