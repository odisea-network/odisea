using System.Globalization;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Notifications.PublicApi;
using Odisea.Modules.Workspace.Domain;
using Odisea.Modules.Workspace.Features.ExternalReservations;
using Odisea.Modules.Workspace.Infrastructure;
using Odisea.SharedKernel;

namespace Odisea.Modules.Workspace.Features.Inquiries;

/// Forms are the agency's (scoped by its id from the token); submissions arrive anonymously through
/// the public endpoint and are answered with success even when dropped as spam, so bots learn nothing.
public sealed partial class InquiryFormService(
    WorkspaceDbContext db,
    IAgencyEntitlements entitlements,
    IEmailOutbox outbox,
    IClock clock,
    ILogger<InquiryFormService> logger)
{
    private const int MaxPageSize = 200;
    private static readonly TimeZoneInfo LocalZone = ResolveZone("Europe/Sofia", "FLE Standard Time");

    [GeneratedRegex(@"^[A-Za-z0-9_\-\.\[\]]{1,60}$")]
    private static partial Regex FieldKey();

    [GeneratedRegex(@"\{\{\s*([A-Za-z0-9_\-\.\[\]]+)\s*\}\}")]
    private static partial Regex Placeholder();

    // ----- forms (agency) -----

    public async Task<InquiryFormDto> CreateFormAsync(Guid agencyId, SaveInquiryFormRequest request, CancellationToken ct)
    {
        await entitlements.RequireAsync(agencyId, AgencyFeature.InquiryForms, ct);
        var form = new InquiryForm { AgencyId = agencyId, Name = request.Name.Trim(), AccessKey = FormAccessKey.New(), CreatedAt = clock.UtcNow };
        Apply(form, request);
        db.InquiryForms.Add(form);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Inquiry form {FormId} created for agency {AgencyId}", form.Id, agencyId);
        return form.ToDto();
    }

    public async Task<List<InquiryFormDto>> ListFormsAsync(Guid agencyId, CancellationToken ct) =>
        (await db.InquiryForms.AsNoTracking().Where(f => f.AgencyId == agencyId).OrderBy(f => f.Name).ToListAsync(ct))
        .Select(f => f.ToDto()).ToList();

    public async Task<InquiryFormDto> GetFormAsync(Guid id, Guid agencyId, CancellationToken ct) =>
        (await LoadFormAsync(id, agencyId, ct)).ToDto();

    public async Task<InquiryFormDto> UpdateFormAsync(Guid id, Guid agencyId, SaveInquiryFormRequest request, CancellationToken ct)
    {
        await entitlements.RequireAsync(agencyId, AgencyFeature.InquiryForms, ct);
        var form = await LoadFormAsync(id, agencyId, ct);
        form.Name = request.Name.Trim();
        Apply(form, request);
        await db.SaveChangesAsync(ct);
        return form.ToDto();
    }

    /// A leaked key is replaced; the old one stops working at once.
    public async Task<InquiryFormDto> RotateKeyAsync(Guid id, Guid agencyId, CancellationToken ct)
    {
        await entitlements.RequireAsync(agencyId, AgencyFeature.InquiryForms, ct);
        var form = await LoadFormAsync(id, agencyId, ct);
        form.AccessKey = FormAccessKey.New();
        form.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Inquiry form {FormId} key rotated", form.Id);
        return form.ToDto();
    }

    public async Task DeleteFormAsync(Guid id, Guid agencyId, CancellationToken ct)
    {
        var form = await LoadFormAsync(id, agencyId, ct);
        var submissions = await db.InquirySubmissions.CountAsync(s => s.FormId == id, ct);
        if (submissions > 0)
            throw new FormInUseException(submissions);
        db.InquiryForms.Remove(form);
        await db.SaveChangesAsync(ct);
    }

    // ----- submissions (public) -----

    public async Task<SubmissionResult> SubmitAsync(
        string? accessKey, IReadOnlyDictionary<string, string> rawFields, SubmissionContext context, CancellationToken ct)
    {
        if (!FormAccessKey.LooksValid(accessKey))
            throw new WorkspaceNotFoundException("The form");
        var form = await db.InquiryForms.FirstOrDefaultAsync(f => f.AccessKey == accessKey && f.IsEnabled, ct)
            ?? throw new WorkspaceNotFoundException("The form");

        // A downgraded or suspended agency's forms simply stop existing for the public.
        var plan = await entitlements.GetAsync(form.AgencyId, ct);
        if (plan is null || !plan.IsActive || !plan.Has(AgencyFeature.InquiryForms))
            throw new WorkspaceNotFoundException("The form");

        var origin = WebOrigin.Normalize(context.Origin) ?? WebOrigin.Normalize(context.Referer);
        if (form.AllowedOrigins.Length > 0 && (origin is null || !form.AllowedOrigins.Contains(origin)))
            throw new OriginNotAllowedException();

        var now = clock.UtcNow;
        if (rawFields.TryGetValue(form.HoneypotField, out var honey) && !string.IsNullOrWhiteSpace(honey))
        {
            form.SpamCount++;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Inquiry form {FormId}: honeypot hit dropped", form.Id);
            return new SubmissionResult(null, Accepted: false, form.AllowedOrigins);
        }

        var fields = CleanFields(rawFields, form.HoneypotField);
        var email = Lookup(fields, form.EmailField) is { } e && MailAddress.TryCreate(e, out var parsed) && parsed.Address == e ? e : null;
        var name = Lookup(fields, form.NameField);

        var submission = new InquirySubmission
        {
            FormId = form.Id,
            AgencyId = form.AgencyId,
            Fields = JsonSerializer.Serialize(fields),
            CustomerName = name is { Length: <= 200 } ? name : name?[..200],
            CustomerEmail = email?.ToLowerInvariant(),
            Origin = origin,
            UserAgent = Truncate(context.UserAgent, 300),
            IpHash = context.RemoteIp is { Length: > 0 } ip ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ip))) : null,
            CreatedAt = now,
        };
        db.InquirySubmissions.Add(submission);
        form.SubmissionCount++;
        await db.SaveChangesAsync(ct);

        if (plan.Has(AgencyFeature.EmailNotifications))
            await NotifyAsync(form, submission, fields, now, ct);

        logger.LogInformation("Inquiry {SubmissionId} received for form {FormId} (agency {AgencyId})", submission.Id, form.Id, form.AgencyId);
        return new SubmissionResult(submission.Id, Accepted: true, form.AllowedOrigins);
    }

    // ----- submissions (agency) -----

    public async Task<PagedResult<InquirySummaryDto>> ListAsync(
        Guid agencyId, Guid? formId, InquiryStatus? status, string? search, DateOnly? from, DateOnly? to,
        int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var query = db.InquirySubmissions.AsNoTracking().Where(s => s.AgencyId == agencyId);
        if (formId is { } f)
            query = query.Where(s => s.FormId == f);
        if (status is { } st)
            query = query.Where(s => s.Status == st);
        else
            query = query.Where(s => s.Status != InquiryStatus.Spam && s.Status != InquiryStatus.Archived);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLowerInvariant();
            query = query.Where(s =>
                (s.CustomerName != null && s.CustomerName.ToLower().Contains(q))
                || (s.CustomerEmail != null && s.CustomerEmail.Contains(q)));
        }
        if (from is { } d1)
            query = query.Where(s => s.CreatedAt >= new DateTimeOffset(d1.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        if (to is { } d2)
            query = query.Where(s => s.CreatedAt < new DateTimeOffset(d2.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(s => s.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<InquirySummaryDto>(items.Select(s => s.ToSummaryDto()).ToList(), page, pageSize, total);
    }

    public async Task<InquiryDto> GetAsync(Guid id, Guid agencyId, CancellationToken ct) =>
        (await LoadSubmissionAsync(id, agencyId, ct)).ToDto();

    public async Task<InquiryDto> SetStatusAsync(Guid id, Guid agencyId, Guid userId, SetInquiryStatusRequest request, CancellationToken ct)
    {
        var submission = await LoadSubmissionAsync(id, agencyId, ct);
        if (!Enum.IsDefined(request.Status))
            throw new WorkspaceValidationException("Unknown status.");
        submission.SetStatus(request.Status, request.Note, userId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return submission.ToDto();
    }

    // ----- helpers -----

    private async Task NotifyAsync(InquiryForm form, InquirySubmission submission, Dictionary<string, string> fields, DateTimeOffset now, CancellationToken ct)
    {
        var received = TimeZoneInfo.ConvertTime(now, LocalZone).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        if (form.NotifyEmails.Length > 0)
        {
            var body = new StringBuilder()
                .AppendLine($"New inquiry via \"{form.Name}\"")
                .AppendLine($"Received: {received}")
                .AppendLine($"From: {submission.Origin ?? "unknown origin"}")
                .AppendLine();
            foreach (var (key, value) in fields)
                body.AppendLine($"{key}: {value}");
            foreach (var to in form.NotifyEmails)
                await outbox.EnqueueAsync(new EmailRequest(
                    to, Truncate($"New inquiry via {form.Name}", 200)!, body.ToString(),
                    ReplyTo: submission.CustomerEmail, Category: "inquiry-notification",
                    AgencyId: form.AgencyId, CorrelationId: submission.Id.ToString()), ct);
        }

        if (form.AutoReplyEnabled && submission.CustomerEmail is { } customer && !string.IsNullOrWhiteSpace(form.AutoReplyBody))
            await outbox.EnqueueAsync(new EmailRequest(
                customer,
                Fill(string.IsNullOrWhiteSpace(form.AutoReplySubject) ? "We received your inquiry" : form.AutoReplySubject, fields),
                Fill(form.AutoReplyBody, fields),
                ReplyTo: form.NotifyEmails.FirstOrDefault(),
                Category: "inquiry-auto-reply", AgencyId: form.AgencyId, CorrelationId: submission.Id.ToString(),
                ToName: submission.CustomerName), ct);
    }

    /// "{{name}}" in auto-reply texts becomes the submitted field value (case-insensitive); unknown fields become empty.
    public static string Fill(string template, Dictionary<string, string> fields) =>
        Placeholder().Replace(template, m => Lookup(fields, m.Groups[1].Value) ?? string.Empty);

    private static string? Lookup(Dictionary<string, string> fields, string key) =>
        fields.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)).Value is { Length: > 0 } v ? v : null;

    public static Dictionary<string, string> CleanFields(IReadOnlyDictionary<string, string> raw, string honeypotField)
    {
        var clean = new Dictionary<string, string>();
        var bytes = 0;
        foreach (var (key, value) in raw)
        {
            if (string.Equals(key, honeypotField, StringComparison.OrdinalIgnoreCase) || PublicSubmission.IsReserved(key))
                continue;
            if (!FieldKey().IsMatch(key))
                throw new WorkspaceValidationException($"Field name '{Truncate(key, 40)}' is not allowed.");
            var v = (value ?? string.Empty).Trim();
            if (v.Length > InquirySubmission.MaxValueLength)
                throw new WorkspaceValidationException($"Field '{key}' exceeds {InquirySubmission.MaxValueLength} characters.");
            bytes += Encoding.UTF8.GetByteCount(key) + Encoding.UTF8.GetByteCount(v);
            if (bytes > InquirySubmission.MaxTotalBytes)
                throw new WorkspaceValidationException("The submission is too large.");
            clean[key] = v;
            if (clean.Count > InquirySubmission.MaxFields)
                throw new WorkspaceValidationException($"At most {InquirySubmission.MaxFields} fields are accepted.");
        }
        if (clean.Count == 0)
            throw new WorkspaceValidationException("The submission has no fields.");
        return clean;
    }

    private void Apply(InquiryForm form, SaveInquiryFormRequest request)
    {
        form.AllowedOrigins = NormalizeOrigins(request.AllowedOrigins);
        form.NotifyEmails = NormalizeEmails(request.NotifyEmails);
        form.AutoReplyEnabled = request.AutoReplyEnabled;
        form.AutoReplySubject = Clean(request.AutoReplySubject);
        form.AutoReplyBody = Clean(request.AutoReplyBody);
        if (form.AutoReplyEnabled && form.AutoReplyBody is null)
            throw new WorkspaceValidationException("An auto-reply needs a body.");
        form.EmailField = FieldName(request.EmailField, "email");
        form.NameField = FieldName(request.NameField, "name");
        form.HoneypotField = FieldName(request.HoneypotField, "botcheck");
        form.IsEnabled = request.IsEnabled;
        form.UpdatedAt = clock.UtcNow;
    }

    private static string[] NormalizeOrigins(string[]? origins)
    {
        if (origins is null)
            return [];
        var clean = new List<string>();
        foreach (var o in origins.Where(o => !string.IsNullOrWhiteSpace(o)))
            clean.Add(WebOrigin.Normalize(o) ?? throw new WorkspaceValidationException($"'{Truncate(o, 60)}' is not a valid origin (use https://www.example.com)."));
        if (clean.Count > InquiryForm.MaxAllowedOrigins)
            throw new WorkspaceValidationException($"At most {InquiryForm.MaxAllowedOrigins} origins are allowed.");
        return clean.Distinct().ToArray();
    }

    private static string[] NormalizeEmails(string[]? emails)
    {
        if (emails is null)
            return [];
        var clean = new List<string>();
        foreach (var e in emails.Select(x => x?.Trim()).Where(x => !string.IsNullOrEmpty(x)))
        {
            if (!MailAddress.TryCreate(e, out var parsed) || parsed.Address != e)
                throw new WorkspaceValidationException($"'{Truncate(e, 60)}' is not a valid e-mail address.");
            clean.Add(e!.ToLowerInvariant());
        }
        if (clean.Count > InquiryForm.MaxNotifyEmails)
            throw new WorkspaceValidationException($"At most {InquiryForm.MaxNotifyEmails} notification addresses are allowed.");
        return clean.Distinct().ToArray();
    }

    private static string FieldName(string? value, string fallback)
    {
        var clean = Clean(value) ?? fallback;
        if (!FieldKey().IsMatch(clean))
            throw new WorkspaceValidationException($"'{Truncate(clean, 40)}' is not a valid field name.");
        return clean;
    }

    private async Task<InquiryForm> LoadFormAsync(Guid id, Guid agencyId, CancellationToken ct) =>
        await db.InquiryForms.FirstOrDefaultAsync(f => f.Id == id && f.AgencyId == agencyId, ct)
        ?? throw new WorkspaceNotFoundException("The form");

    private async Task<InquirySubmission> LoadSubmissionAsync(Guid id, Guid agencyId, CancellationToken ct) =>
        await db.InquirySubmissions.FirstOrDefaultAsync(s => s.Id == id && s.AgencyId == agencyId, ct)
        ?? throw new WorkspaceNotFoundException("The inquiry");

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Truncate(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];

    private static TimeZoneInfo ResolveZone(string ianaId, string windowsId)
    {
        foreach (var id in new[] { ianaId, windowsId })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }
        return TimeZoneInfo.Utc;
    }
}
