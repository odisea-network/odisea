using System.ComponentModel.DataAnnotations;
using Odisea.Modules.Documents.Domain;

namespace Odisea.Modules.Documents.Features.Contracts;

// CustomFields: placeholders the API cannot fill by itself; each must come in GenerateContractRequest.Fields.
public sealed record ContractTemplateDto(
    Guid Id,
    string Name,
    ContractKind Kind,
    string? Description,
    string FileName,
    int FileSize,
    int Version,
    bool IsActive,
    bool AgencyUsable,
    string[] Placeholders,
    string[] CustomFields,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record UpdateContractTemplateRequest(
    [Required, MaxLength(100)] string Name,
    ContractKind Kind,
    [MaxLength(500)] string? Description,
    bool IsActive,
    bool AgencyUsable);

public sealed record PlaceholderDto(string Name, string Group, string Description, bool Repeating);

// Fields: values for the template's custom fields, by placeholder name. Data-backed placeholders cannot be overridden.
public sealed record GenerateContractRequest(
    Guid TemplateId,
    Guid? BookingId,
    Guid? AgencyId,
    Dictionary<string, string>? Fields,
    [MaxLength(500)] string? Note);

/// Agency self-service: the booking must belong to the caller's agency, the template must be agency-usable.
public sealed record AgencyGenerateContractRequest(
    Guid TemplateId,
    Guid BookingId,
    Dictionary<string, string>? Fields);

public sealed record GeneratedContractDto(
    Guid Id,
    Guid TemplateId,
    int TemplateVersion,
    string TemplateName,
    ContractKind Kind,
    Guid? AgencyId,
    Guid? BookingId,
    string? BookingRef,
    string FileName,
    int FileSize,
    Guid GeneratedByUserId,
    bool GeneratedByAgency,
    string? Note,
    bool SharedWithAgency,
    DateTimeOffset? SharedAt,
    DateTimeOffset CreatedAt);

public sealed record MergeDataDto(
    Dictionary<string, string> Scalars,
    Dictionary<string, List<Dictionary<string, string>>> Collections);

public sealed record ContractFile(byte[] Content, string FileName);

public static class ContractDtoMapping
{
    public const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public static ContractTemplateDto ToDto(this ContractTemplate t) => new(
        t.Id, t.Name, t.Kind, t.Description, t.FileName, t.FileSize, t.Version, t.IsActive, t.AgencyUsable,
        t.Placeholders, PlaceholderCatalogue.CustomFields(t.Placeholders), t.CreatedAt, t.UpdatedAt);

    public static GeneratedContractDto ToDto(this GeneratedContract c) => new(
        c.Id, c.TemplateId, c.TemplateVersion, c.TemplateName, c.Kind, c.AgencyId, c.BookingId, c.BookingRef,
        c.FileName, c.FileSize, c.GeneratedByUserId, c.GeneratedByAgency, c.Note, c.SharedWithAgency, c.SharedAt,
        c.CreatedAt);

    public static MergeDataDto ToDto(this MergeData data) => new(data.Scalars, data.Collections);

    public static PlaceholderDto ToDto(this PlaceholderDefinition p) => new(p.Name, p.Group, p.Description, p.Repeating);
}
