using Odisea.SharedKernel;

namespace Odisea.Modules.Documents.Domain;

/// An operator-authored .docx whose {{Placeholders}} the API fills from booking, agency and
/// company data. The bytes live in the database next to the documents they produce.
public class ContractTemplate : Entity
{
    public required string Name { get; set; }
    public ContractKind Kind { get; set; } = ContractKind.Other;
    public string? Description { get; set; }

    public required string FileName { get; set; }
    public required byte[] Content { get; set; }
    public required int FileSize { get; set; }
    public required string ContentHash { get; set; }

    /// Bumped on every file replacement; generated contracts record the version they used.
    public int Version { get; set; } = 1;

    /// Placeholder names found in the file at upload time, in order of first appearance.
    public string[] Placeholders { get; set; } = [];

    public bool IsActive { get; set; } = true;

    /// Agencies may generate this template themselves for their own bookings (customer contracts).
    public bool AgencyUsable { get; set; }
    public required Guid UploadedByUserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum ContractKind
{
    /// Operator-to-agency commercial agreement.
    AgencyAgreement,
    /// Travel contract for the travellers of one booking.
    CustomerContract,
    Other,
}
