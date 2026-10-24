using Odisea.SharedKernel;

namespace Odisea.Modules.Documents.Domain;

/// A filled contract. Immutable once generated: the file is the record of what was handed out.
public class GeneratedContract : Entity
{
    public required Guid TemplateId { get; set; }
    public required int TemplateVersion { get; set; }
    public required string TemplateName { get; set; }
    public ContractKind Kind { get; set; }

    public Guid? AgencyId { get; set; }
    public Guid? BookingId { get; set; }
    public string? BookingRef { get; set; }

    public required string FileName { get; set; }
    public required byte[] Content { get; set; }
    public required int FileSize { get; set; }

    public required Guid GeneratedByUserId { get; set; }

    /// True when an agency user or API key generated it (self-service); such contracts are shared at once.
    public bool GeneratedByAgency { get; set; }
    public string? Note { get; set; }

    /// Agencies only see contracts the operator explicitly shared.
    public bool SharedWithAgency { get; set; }
    public DateTimeOffset? SharedAt { get; set; }
}

public sealed class ContractValidationException(string message) : DomainException(message);

public sealed class ContractNotFoundException(string what) : DomainException($"{what} was not found.");

public sealed class ContractTemplateInUseException(int contracts)
    : DomainException($"The template was used for {contracts} contract(s); deactivate it instead of deleting.");
