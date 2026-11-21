using Odisea.SharedKernel;

namespace Odisea.Modules.Workspace.Domain.Ledger;

/// A supplier the agency books with. The agency's own list: nothing here is shared between agencies
/// or tied to the platform operator that runs this deployment.
public class TourOperator : Entity
{
    public required Guid AgencyId { get; init; }
    public required string Name { get; set; }

    /// Lower-cased name; the unique index sits on this so "Alfa Tours" and "alfa tours" are one operator.
    public required string NameKey { get; set; }

    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Notes { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public static string KeyOf(string name) => name.Trim().ToLowerInvariant();
}
