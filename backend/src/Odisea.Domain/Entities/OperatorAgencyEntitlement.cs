using Odisea.Domain.Common;
using Odisea.Domain.Enums;

namespace Odisea.Domain.Entities;

// Governs whether an agency may distribute a given operator's PlatformShared
// offers, and on what commercial terms. Without an Active entitlement an agency
// can only build Collections over its own AgencyPrivate offers. One row per
// (operator, agency) pair.
public class OperatorAgencyEntitlement : Entity
{
    public Guid OperatorId { get; set; }
    public Operator? Operator { get; set; }

    public Guid AgencyId { get; set; }
    public Agency? Agency { get; set; }

    public EntitlementStatus Status { get; set; } = EntitlementStatus.Active;

    // Operator's commission on bookings the agency drives, in percent (0–100).
    // Stored on the entitlement so it can differ per agency relationship.
    public decimal CommissionPercent { get; set; }
}
