using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Booking.PublicApi;
using Odisea.Modules.Documents.Domain;
using Odisea.Modules.Documents.Infrastructure;
using Odisea.SharedKernel;

namespace Odisea.Modules.Documents.Features.Contracts;

public partial class ContractService(
    DocumentsDbContext db,
    DocxTemplateRenderer renderer,
    ContractMergeDataBuilder mergeData,
    IBookingLookup bookingLookup,
    IAgencyLookup agencyLookup,
    IClock clock,
    ILogger<ContractService> logger)
{
    private const int MaxFields = 50;
    private const int MaxFieldLength = 4000;

    /// Self-service guard: contracts carry traveller data, so an agency cannot mint them without limit.
    public const int MaxAgencyContractsPerBooking = 20;

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z0-9_]+)*$")]
    private static partial Regex FieldName();

    [GeneratedRegex("-{2,}")]
    private static partial Regex Dashes();

    // ----- templates (operator) -----

    public async Task<List<ContractTemplateDto>> ListTemplatesAsync(bool includeInactive, CancellationToken ct)
    {
        var query = db.ContractTemplates.AsNoTracking();
        if (!includeInactive)
            query = query.Where(t => t.IsActive);
        var items = await query.OrderBy(t => t.Name).Select(TemplateProjection).ToListAsync(ct);
        return items.Select(WithCustomFields).ToList();
    }

    public async Task<ContractTemplateDto> GetTemplateAsync(Guid id, CancellationToken ct)
    {
        var dto = await db.ContractTemplates.AsNoTracking().Where(t => t.Id == id)
            .Select(TemplateProjection).FirstOrDefaultAsync(ct)
            ?? throw new ContractNotFoundException("The template");
        return WithCustomFields(dto);
    }

    public async Task<ContractFile> GetTemplateFileAsync(Guid id, CancellationToken ct) =>
        await db.ContractTemplates.AsNoTracking().Where(t => t.Id == id)
            .Select(t => new ContractFile(t.Content, t.FileName)).FirstOrDefaultAsync(ct)
        ?? throw new ContractNotFoundException("The template");

    public async Task<ContractTemplateDto> UploadTemplateAsync(
        string? name, string? kind, string? description, bool agencyUsable,
        byte[] bytes, string? fileName, Guid userId, CancellationToken ct)
    {
        var inspection = renderer.Inspect(bytes);
        var template = new ContractTemplate
        {
            Name = RequireName(name),
            Kind = ParseKind(kind),
            Description = CleanDescription(description),
            AgencyUsable = agencyUsable,
            FileName = SafeFileName(fileName, "template.docx"),
            Content = bytes,
            FileSize = bytes.Length,
            ContentHash = Hash(bytes),
            Placeholders = [.. inspection.Placeholders],
            UploadedByUserId = userId,
            UpdatedAt = clock.UtcNow,
        };
        db.ContractTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Contract template {TemplateId} '{Name}' uploaded: {Bytes} bytes, {Placeholders} placeholder(s)",
            template.Id, template.Name, template.FileSize, template.Placeholders.Length);
        return template.ToDto();
    }

    public async Task<ContractTemplateDto> ReplaceTemplateFileAsync(Guid id, byte[] bytes, string? fileName, CancellationToken ct)
    {
        var template = await db.ContractTemplates.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new ContractNotFoundException("The template");
        var inspection = renderer.Inspect(bytes);
        template.Content = bytes;
        template.FileSize = bytes.Length;
        template.ContentHash = Hash(bytes);
        template.FileName = SafeFileName(fileName, template.FileName);
        template.Placeholders = [.. inspection.Placeholders];
        template.Version++;
        template.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Contract template {TemplateId} replaced, now v{Version}", template.Id, template.Version);
        return template.ToDto();
    }

    public async Task<ContractTemplateDto> UpdateTemplateAsync(Guid id, UpdateContractTemplateRequest request, CancellationToken ct)
    {
        var template = await db.ContractTemplates.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new ContractNotFoundException("The template");
        template.Name = RequireName(request.Name);
        template.Kind = request.Kind;
        template.Description = CleanDescription(request.Description);
        template.IsActive = request.IsActive;
        template.AgencyUsable = request.AgencyUsable;
        template.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return template.ToDto();
    }

    public async Task DeleteTemplateAsync(Guid id, CancellationToken ct)
    {
        var template = await db.ContractTemplates.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new ContractNotFoundException("The template");
        var used = await db.GeneratedContracts.CountAsync(c => c.TemplateId == id, ct);
        if (used > 0)
            throw new ContractTemplateInUseException(used);
        db.ContractTemplates.Remove(template);
        await db.SaveChangesAsync(ct);
    }

    // ----- generation (operator) -----

    /// What a template would receive for these subjects; lets the operator check data before generating.
    public async Task<MergeDataDto> PreviewDataAsync(Guid? bookingId, Guid? agencyId, CancellationToken ct)
    {
        var (agency, booking) = await ResolveSubjectsAsync(bookingId, agencyId, ct);
        var company = await db.CompanyProfiles.AsNoTracking().FirstOrDefaultAsync(ct);
        return mergeData.Build(company, agency, booking, fields: null).ToDto();
    }

    public async Task<GeneratedContractDto> GenerateAsync(GenerateContractRequest request, Guid userId, CancellationToken ct)
    {
        var template = await db.ContractTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.TemplateId, ct)
            ?? throw new ContractNotFoundException("The template");
        if (!template.IsActive)
            throw new ContractValidationException("The template is inactive.");

        var fields = ValidateFields(request.Fields);
        var (agency, booking) = await ResolveSubjectsAsync(request.BookingId, request.AgencyId, ct);
        var content = await RenderAsync(template, agency, booking, fields, ct);

        var contract = NewContract(template, agency, booking, content, userId);
        contract.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        return await SaveAsync(contract, ct);
    }

    public async Task<List<GeneratedContractDto>> ListGeneratedAsync(
        Guid? bookingId, Guid? agencyId, Guid? templateId, CancellationToken ct)
    {
        var query = db.GeneratedContracts.AsNoTracking();
        if (bookingId is { } b)
            query = query.Where(c => c.BookingId == b);
        if (agencyId is { } a)
            query = query.Where(c => c.AgencyId == a);
        if (templateId is { } t)
            query = query.Where(c => c.TemplateId == t);
        return await query.OrderByDescending(c => c.CreatedAt).Take(200).Select(GeneratedProjection).ToListAsync(ct);
    }

    public async Task<GeneratedContractDto> GetGeneratedAsync(Guid id, CancellationToken ct) =>
        await db.GeneratedContracts.AsNoTracking().Where(c => c.Id == id).Select(GeneratedProjection).FirstOrDefaultAsync(ct)
        ?? throw new ContractNotFoundException("The contract");

    public async Task<ContractFile> GetGeneratedFileAsync(Guid id, CancellationToken ct) =>
        await db.GeneratedContracts.AsNoTracking().Where(c => c.Id == id)
            .Select(c => new ContractFile(c.Content, c.FileName)).FirstOrDefaultAsync(ct)
        ?? throw new ContractNotFoundException("The contract");

    public async Task<GeneratedContractDto> SetSharedAsync(Guid id, bool shared, CancellationToken ct)
    {
        var contract = await db.GeneratedContracts.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new ContractNotFoundException("The contract");
        if (contract.AgencyId is null)
            throw new ContractValidationException("The contract is not linked to an agency.");
        contract.SharedWithAgency = shared;
        contract.SharedAt = shared ? clock.UtcNow : null;
        await db.SaveChangesAsync(ct);
        return contract.ToDto();
    }

    // ----- agency side: own bookings, shared contracts, agency-usable templates only -----

    public async Task<List<ContractTemplateDto>> ListTemplatesForAgencyAsync(CancellationToken ct)
    {
        var items = await db.ContractTemplates.AsNoTracking()
            .Where(t => t.IsActive && t.AgencyUsable)
            .OrderBy(t => t.Name).Select(TemplateProjection).ToListAsync(ct);
        return items.Select(WithCustomFields).ToList();
    }

    public async Task<GeneratedContractDto> GenerateForAgencyAsync(
        AgencyGenerateContractRequest request, Guid agencyId, Guid userId, CancellationToken ct)
    {
        // Not-found for anything outside the caller's reach: never confirm what exists elsewhere.
        var template = await db.ContractTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId && t.IsActive && t.AgencyUsable, ct)
            ?? throw new ContractNotFoundException("The template");
        var fields = ValidateFields(request.Fields);
        var booking = await bookingLookup.GetContractInfoAsync(request.BookingId, ct);
        if (booking is null || booking.AgencyId != agencyId)
            throw new ContractNotFoundException("The booking");

        var already = await db.GeneratedContracts.CountAsync(c => c.BookingId == booking.Id && c.GeneratedByAgency, ct);
        if (already >= MaxAgencyContractsPerBooking)
            throw new ContractValidationException(
                $"At most {MaxAgencyContractsPerBooking} contracts can be generated per booking; contact the operator.");

        var agency = await agencyLookup.GetContractInfoAsync(agencyId, ct)
            ?? throw new ContractNotFoundException("The agency");
        var content = await RenderAsync(template, agency, booking, fields, ct);

        var contract = NewContract(template, agency, booking, content, userId);
        contract.GeneratedByAgency = true;
        contract.SharedWithAgency = true;
        contract.SharedAt = clock.UtcNow;
        return await SaveAsync(contract, ct);
    }

    public Task<List<GeneratedContractDto>> ListForAgencyAsync(Guid agencyId, CancellationToken ct) =>
        SharedWith(agencyId).OrderByDescending(c => c.CreatedAt).Select(GeneratedProjection).ToListAsync(ct);

    public async Task<GeneratedContractDto> GetForAgencyAsync(Guid id, Guid agencyId, CancellationToken ct) =>
        await SharedWith(agencyId).Where(c => c.Id == id).Select(GeneratedProjection).FirstOrDefaultAsync(ct)
        ?? throw new ContractNotFoundException("The contract");

    public async Task<ContractFile> GetFileForAgencyAsync(Guid id, Guid agencyId, CancellationToken ct) =>
        await SharedWith(agencyId).Where(c => c.Id == id)
            .Select(c => new ContractFile(c.Content, c.FileName)).FirstOrDefaultAsync(ct)
        ?? throw new ContractNotFoundException("The contract");

    private IQueryable<GeneratedContract> SharedWith(Guid agencyId) =>
        db.GeneratedContracts.AsNoTracking().Where(c => c.AgencyId == agencyId && c.SharedWithAgency);

    // ----- helpers -----

    private async Task<byte[]> RenderAsync(
        ContractTemplate template, AgencyContractInfo? agency, BookingContractInfo? booking,
        Dictionary<string, string>? fields, CancellationToken ct)
    {
        var company = await db.CompanyProfiles.AsNoTracking().FirstOrDefaultAsync(ct);
        var rendered = renderer.Render(template.Content, mergeData.Build(company, agency, booking, fields));
        if (rendered.Unresolved.Count > 0)
            throw new ContractValidationException(
                $"The template still has unfilled placeholders: {string.Join(", ", rendered.Unresolved)}. " +
                "Supply them in 'fields', or generate for a booking/agency that provides them.");
        return rendered.Content;
    }

    private GeneratedContract NewContract(
        ContractTemplate template, AgencyContractInfo? agency, BookingContractInfo? booking, byte[] content, Guid userId) =>
        new()
        {
            TemplateId = template.Id,
            TemplateVersion = template.Version,
            TemplateName = template.Name,
            Kind = template.Kind,
            AgencyId = agency?.Id,
            BookingId = booking?.Id,
            BookingRef = booking?.Ref,
            FileName = BuildFileName(template.Name, booking?.Ref ?? agency?.Name),
            Content = content,
            FileSize = content.Length,
            GeneratedByUserId = userId,
        };

    private async Task<GeneratedContractDto> SaveAsync(GeneratedContract contract, CancellationToken ct)
    {
        db.GeneratedContracts.Add(contract);
        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Contract {ContractId} generated from template {TemplateId} v{Version} for booking {BookingRef}, agency {AgencyId}, by agency: {ByAgency}",
            contract.Id, contract.TemplateId, contract.TemplateVersion, contract.BookingRef, contract.AgencyId, contract.GeneratedByAgency);
        return contract.ToDto();
    }

    private async Task<(AgencyContractInfo? Agency, BookingContractInfo? Booking)> ResolveSubjectsAsync(
        Guid? bookingId, Guid? agencyId, CancellationToken ct)
    {
        BookingContractInfo? booking = null;
        if (bookingId is { } b)
            booking = await bookingLookup.GetContractInfoAsync(b, ct)
                ?? throw new ContractNotFoundException("The booking");
        if (booking is not null && agencyId is { } requested && requested != booking.AgencyId)
            throw new ContractValidationException("The booking belongs to a different agency than the one requested.");

        AgencyContractInfo? agency = null;
        if ((agencyId ?? booking?.AgencyId) is { } id)
            agency = await agencyLookup.GetContractInfoAsync(id, ct)
                ?? throw new ContractNotFoundException("The agency");
        return (agency, booking);
    }

    private static Dictionary<string, string>? ValidateFields(Dictionary<string, string>? fields)
    {
        if (fields is null || fields.Count == 0)
            return null;
        if (fields.Count > MaxFields)
            throw new ContractValidationException($"At most {MaxFields} custom fields are accepted.");

        var clean = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in fields)
        {
            if (!FieldName().IsMatch(name))
                throw new ContractValidationException($"'{name}' is not a valid placeholder name.");
            if (PlaceholderCatalogue.IsKnown(name))
                throw new ContractValidationException($"'{name}' is filled from booking data and cannot be overridden.");
            if (value is null)
                throw new ContractValidationException($"Field '{name}' has no value.");
            if (value.Length > MaxFieldLength)
                throw new ContractValidationException($"Field '{name}' exceeds {MaxFieldLength} characters.");
            clean[name] = value;
        }
        return clean;
    }

    private static string RequireName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ContractValidationException("A template name is required.");
        if (trimmed.Length > 100)
            throw new ContractValidationException("The template name exceeds 100 characters.");
        return trimmed;
    }

    private static string? CleanDescription(string? description)
    {
        var trimmed = description?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        if (trimmed.Length > 500)
            throw new ContractValidationException("The description exceeds 500 characters.");
        return trimmed;
    }

    private static ContractKind ParseKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
            return ContractKind.Other;
        return Enum.TryParse<ContractKind>(kind, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new ContractValidationException(
                $"Unknown contract kind '{kind}'. Use {string.Join(", ", Enum.GetNames<ContractKind>())}.");
    }

    private static string SafeFileName(string? fileName, string fallback)
    {
        var name = Path.GetFileName(fileName ?? "").Trim();
        if (name.Length == 0)
            return fallback;
        if (!name.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            name += ".docx";
        return name.Length > 255 ? name[^255..] : name;
    }

    private string BuildFileName(string templateName, string? subject)
    {
        // Same calendar day the document itself prints as {{Today}}.
        var stamp = TimeZoneInfo.ConvertTime(clock.UtcNow, ContractMergeDataBuilder.LocalZone).ToString("yyyyMMdd");
        var parts = new[] { Slug(templateName), Slug(subject ?? ""), stamp }.Where(p => p.Length > 0);
        return string.Join("-", parts) + ".docx";
    }

    private static string Slug(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
            builder.Append(char.IsLetterOrDigit(ch) ? ch : '-');
        return Dashes().Replace(builder.ToString(), "-").Trim('-');
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    // Projections leave the file bytes in the database; CustomFields is derived afterwards from the catalogue.
    private static readonly Expression<Func<ContractTemplate, ContractTemplateDto>> TemplateProjection =
        t => new ContractTemplateDto(
            t.Id, t.Name, t.Kind, t.Description, t.FileName, t.FileSize, t.Version, t.IsActive, t.AgencyUsable,
            t.Placeholders, Array.Empty<string>(), t.CreatedAt, t.UpdatedAt);

    private static ContractTemplateDto WithCustomFields(ContractTemplateDto dto) =>
        dto with { CustomFields = PlaceholderCatalogue.CustomFields(dto.Placeholders) };

    private static readonly Expression<Func<GeneratedContract, GeneratedContractDto>> GeneratedProjection =
        c => new GeneratedContractDto(
            c.Id, c.TemplateId, c.TemplateVersion, c.TemplateName, c.Kind, c.AgencyId, c.BookingId, c.BookingRef,
            c.FileName, c.FileSize, c.GeneratedByUserId, c.GeneratedByAgency, c.Note, c.SharedWithAgency, c.SharedAt,
            c.CreatedAt);
}
