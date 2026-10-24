using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Notifications.PublicApi;
using Odisea.Modules.Workspace.Domain;
using Odisea.Modules.Workspace.Features.Inquiries;
using Odisea.Modules.Workspace.Infrastructure;
using Odisea.SharedKernel;
using Xunit;

namespace Odisea.UnitTests.Workspace;

public class InquiryFormServiceTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeEntitlements : IAgencyEntitlements
    {
        public HashSet<Guid> Workspace { get; } = [];
        public HashSet<Guid> Suspended { get; } = [];

        public Task<AgencyEntitlements?> GetAsync(Guid agencyId, CancellationToken ct) =>
            Task.FromResult<AgencyEntitlements?>(new AgencyEntitlements(agencyId,
                Workspace.Contains(agencyId) ? "Workspace" : "Core", !Suspended.Contains(agencyId),
                Workspace.Contains(agencyId) ? Enum.GetValues<AgencyFeature>() : []));

        public Task RequireAsync(Guid agencyId, AgencyFeature feature, CancellationToken ct)
        {
            if (Suspended.Contains(agencyId))
                throw new AgencySuspendedException();
            if (!Workspace.Contains(agencyId))
                throw new FeatureNotInPlanException(feature, "Core");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOutbox : IEmailOutbox
    {
        public List<EmailRequest> Sent { get; } = [];
        public Task<Guid> EnqueueAsync(EmailRequest request, CancellationToken ct)
        {
            Sent.Add(request);
            return Task.FromResult(Guid.NewGuid());
        }
    }

    private readonly WorkspaceDbContext _db;
    private readonly FakeClock _clock = new();
    private readonly FakeEntitlements _entitlements = new();
    private readonly FakeOutbox _outbox = new();
    private readonly InquiryFormService _service;
    private readonly Guid _agencyId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private static readonly SubmissionContext FromSite = new("https://www.bluehorizon.example", null, "Mozilla/5.0", "203.0.113.7");

    public InquiryFormServiceTests()
    {
        _db = new WorkspaceDbContext(new DbContextOptionsBuilder<WorkspaceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _service = new InquiryFormService(_db, _entitlements, _outbox, _clock, NullLogger<InquiryFormService>.Instance);
        _entitlements.Workspace.Add(_agencyId);
    }

    private static SaveInquiryFormRequest Form(string[]? origins = null, string[]? notify = null, bool autoReply = true) => new(
        "Website contact", origins, notify ?? ["office@bluehorizon.example", "SALES@bluehorizon.example"],
        autoReply, "Thanks {{name}}", "Hello {{ name }}, we got your request about {{destination}}. {{missing}}",
        null, null, null);

    private Task<InquiryFormDto> CreateFormAsync(SaveInquiryFormRequest? request = null, Guid? agencyId = null) =>
        _service.CreateFormAsync(agencyId ?? _agencyId, request ?? Form(), CancellationToken.None);

    private static Dictionary<string, string> Fields(string? honeypot = null) => new()
    {
        ["name"] = "Georgi Ivanov",
        ["email"] = "Georgi@Example.com",
        ["destination"] = "Rhodes",
        ["message"] = "Two adults, 7 nights in June.",
        ["access_key"] = "ignored",
        ["botcheck"] = honeypot ?? "",
    };

    [Fact]
    public async Task Create_form_generates_an_access_key_and_normalises_configuration()
    {
        var dto = await CreateFormAsync(Form(origins: ["HTTPS://www.BlueHorizon.example/contact?x=1", "http://localhost:4200"]));

        Assert.True(FormAccessKey.LooksValid(dto.AccessKey));
        Assert.Equal($"/api/v1/public/forms/{dto.AccessKey}", dto.SubmitPath);
        Assert.Equal(["https://www.bluehorizon.example", "http://localhost:4200"], dto.AllowedOrigins);
        Assert.Equal(["office@bluehorizon.example", "sales@bluehorizon.example"], dto.NotifyEmails);
        Assert.Equal(("email", "name", "botcheck"), (dto.EmailField, dto.NameField, dto.HoneypotField));
        Assert.True(dto.IsEnabled);
    }

    [Fact]
    public async Task Form_configuration_is_validated_and_gated()
    {
        await Assert.ThrowsAsync<WorkspaceValidationException>(() => CreateFormAsync(Form(origins: ["not a url"])));
        await Assert.ThrowsAsync<WorkspaceValidationException>(() => CreateFormAsync(Form(notify: ["nope"])));
        await Assert.ThrowsAsync<WorkspaceValidationException>(() => CreateFormAsync(
            new SaveInquiryFormRequest("x", null, null, true, null, null, null, null, null)));
        await Assert.ThrowsAsync<FeatureNotInPlanException>(() => CreateFormAsync(agencyId: Guid.NewGuid()));
        Assert.Empty(_db.InquiryForms);
    }

    [Fact]
    public async Task Submission_stores_fields_extracts_customer_and_sends_notifications_and_auto_reply()
    {
        var form = await CreateFormAsync();

        var result = await _service.SubmitAsync(form.AccessKey, Fields(), FromSite, CancellationToken.None);

        Assert.True(result.Accepted);
        var inquiry = await _service.GetAsync(result.SubmissionId!.Value, _agencyId, CancellationToken.None);
        Assert.Equal("Georgi Ivanov", inquiry.CustomerName);
        Assert.Equal("georgi@example.com", inquiry.CustomerEmail);
        Assert.Equal("https://www.bluehorizon.example", inquiry.Origin);
        Assert.Equal(InquiryStatus.New, inquiry.Status);
        Assert.Equal("Rhodes", inquiry.Fields.GetProperty("destination").GetString());
        Assert.False(inquiry.Fields.TryGetProperty("access_key", out _));
        Assert.False(inquiry.Fields.TryGetProperty("botcheck", out _));

        Assert.Equal(3, _outbox.Sent.Count);
        var notifications = _outbox.Sent.Where(m => m.Category == "inquiry-notification").ToList();
        Assert.Equal(["office@bluehorizon.example", "sales@bluehorizon.example"], notifications.Select(m => m.To));
        Assert.All(notifications, m =>
        {
            Assert.Equal("georgi@example.com", m.ReplyTo);
            Assert.Contains("destination: Rhodes", m.TextBody);
            Assert.Equal(result.SubmissionId.ToString(), m.CorrelationId);
            Assert.Equal(_agencyId, m.AgencyId);
        });
        var reply = Assert.Single(_outbox.Sent, m => m.Category == "inquiry-auto-reply");
        Assert.Equal("georgi@example.com", reply.To);
        Assert.Equal("Thanks Georgi Ivanov", reply.Subject);
        Assert.Equal("Hello Georgi Ivanov, we got your request about Rhodes. ", reply.TextBody);
        Assert.Equal("office@bluehorizon.example", reply.ReplyTo);

        var updated = await _service.GetFormAsync(form.Id, _agencyId, CancellationToken.None);
        Assert.Equal((1, 0), (updated.SubmissionCount, updated.SpamCount));
    }

    [Fact]
    public async Task Honeypot_hits_are_dropped_but_answered_as_success()
    {
        var form = await CreateFormAsync();

        var result = await _service.SubmitAsync(form.AccessKey, Fields(honeypot: "http://spam.example"), FromSite, CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Null(result.SubmissionId);
        Assert.Empty(_db.InquirySubmissions);
        Assert.Empty(_outbox.Sent);
        Assert.Equal(1, (await _service.GetFormAsync(form.Id, _agencyId, CancellationToken.None)).SpamCount);
    }

    [Fact]
    public async Task Origin_allowlist_blocks_other_sites_and_missing_origins_but_accepts_referer()
    {
        var form = await CreateFormAsync(Form(origins: ["https://www.bluehorizon.example"]));

        await Assert.ThrowsAsync<OriginNotAllowedException>(() => _service.SubmitAsync(form.AccessKey, Fields(),
            new SubmissionContext("https://evil.example", null, null, null), CancellationToken.None));
        await Assert.ThrowsAsync<OriginNotAllowedException>(() => _service.SubmitAsync(form.AccessKey, Fields(),
            new SubmissionContext(null, null, null, null), CancellationToken.None));
        var viaReferer = await _service.SubmitAsync(form.AccessKey, Fields(),
            new SubmissionContext(null, "https://www.bluehorizon.example/contact.html", null, null), CancellationToken.None);
        Assert.True(viaReferer.Accepted);

        var open = await CreateFormAsync(Form(origins: []));
        Assert.True((await _service.SubmitAsync(open.AccessKey, Fields(), new SubmissionContext(null, null, null, null), CancellationToken.None)).Accepted);
    }

    [Fact]
    public async Task Unknown_disabled_or_unentitled_forms_do_not_exist_for_the_public()
    {
        var form = await CreateFormAsync();
        await Assert.ThrowsAsync<WorkspaceNotFoundException>(() => _service.SubmitAsync("odf_nope", Fields(), FromSite, CancellationToken.None));
        await Assert.ThrowsAsync<WorkspaceNotFoundException>(() => _service.SubmitAsync(FormAccessKey.New(), Fields(), FromSite, CancellationToken.None));

        await _service.UpdateFormAsync(form.Id, _agencyId, Form() with { IsEnabled = false }, CancellationToken.None);
        await Assert.ThrowsAsync<WorkspaceNotFoundException>(() => _service.SubmitAsync(form.AccessKey, Fields(), FromSite, CancellationToken.None));

        await _service.UpdateFormAsync(form.Id, _agencyId, Form(), CancellationToken.None);
        _entitlements.Workspace.Remove(_agencyId);
        await Assert.ThrowsAsync<WorkspaceNotFoundException>(() => _service.SubmitAsync(form.AccessKey, Fields(), FromSite, CancellationToken.None));
        Assert.Empty(_db.InquirySubmissions);
    }

    [Fact]
    public async Task Submission_limits_are_enforced_and_reserved_keys_stripped()
    {
        var form = await CreateFormAsync();
        var tooMany = Enumerable.Range(0, InquirySubmission.MaxFields + 1).ToDictionary(i => $"f{i}", i => "x");
        var badKey = new Dictionary<string, string> { ["<script>"] = "x" };
        var huge = new Dictionary<string, string> { ["message"] = new string('x', InquirySubmission.MaxValueLength + 1) };
        var empty = new Dictionary<string, string> { ["access_key"] = "k", ["redirect"] = "https://x" };

        await Assert.ThrowsAsync<WorkspaceValidationException>(() => _service.SubmitAsync(form.AccessKey, tooMany, FromSite, CancellationToken.None));
        await Assert.ThrowsAsync<WorkspaceValidationException>(() => _service.SubmitAsync(form.AccessKey, badKey, FromSite, CancellationToken.None));
        await Assert.ThrowsAsync<WorkspaceValidationException>(() => _service.SubmitAsync(form.AccessKey, huge, FromSite, CancellationToken.None));
        await Assert.ThrowsAsync<WorkspaceValidationException>(() => _service.SubmitAsync(form.AccessKey, empty, FromSite, CancellationToken.None));
        Assert.Empty(_db.InquirySubmissions);
    }

    [Fact]
    public async Task Rotating_the_key_invalidates_the_old_one_and_used_forms_cannot_be_deleted()
    {
        var form = await CreateFormAsync();
        await _service.SubmitAsync(form.AccessKey, Fields(), FromSite, CancellationToken.None);

        var rotated = await _service.RotateKeyAsync(form.Id, _agencyId, CancellationToken.None);
        Assert.NotEqual(form.AccessKey, rotated.AccessKey);
        await Assert.ThrowsAsync<WorkspaceNotFoundException>(() => _service.SubmitAsync(form.AccessKey, Fields(), FromSite, CancellationToken.None));
        Assert.True((await _service.SubmitAsync(rotated.AccessKey, Fields(), FromSite, CancellationToken.None)).Accepted);

        await Assert.ThrowsAsync<FormInUseException>(() => _service.DeleteFormAsync(form.Id, _agencyId, CancellationToken.None));
        var unused = await CreateFormAsync();
        await _service.DeleteFormAsync(unused.Id, _agencyId, CancellationToken.None);
        Assert.Single(_db.InquiryForms);
    }

    [Fact]
    public async Task Inquiries_are_listed_filtered_triaged_and_scoped_to_the_agency()
    {
        var form = await CreateFormAsync();
        var first = await _service.SubmitAsync(form.AccessKey, Fields(), FromSite, CancellationToken.None);
        _clock.UtcNow = _clock.UtcNow.AddDays(1);
        var second = await _service.SubmitAsync(form.AccessKey, new Dictionary<string, string> { ["name"] = "Maria", ["email"] = "maria@example.com" }, FromSite, CancellationToken.None);

        var read = await _service.SetStatusAsync(first.SubmissionId!.Value, _agencyId, _userId, new SetInquiryStatusRequest(InquiryStatus.Read, "called back"), CancellationToken.None);
        Assert.Equal((InquiryStatus.Read, _userId, "called back"), (read.Status, read.HandledByUserId, read.InternalNote));
        Assert.Equal(_clock.UtcNow, read.HandledAt);
        await _service.SetStatusAsync(second.SubmissionId!.Value, _agencyId, _userId, new SetInquiryStatusRequest(InquiryStatus.Spam, null), CancellationToken.None);

        var open = await _service.ListAsync(_agencyId, null, null, null, null, null, 1, 50, CancellationToken.None);
        var spam = await _service.ListAsync(_agencyId, form.Id, InquiryStatus.Spam, null, null, null, 1, 50, CancellationToken.None);
        var byName = await _service.ListAsync(_agencyId, null, InquiryStatus.Read, "georgi", null, null, 1, 50, CancellationToken.None);
        var today = await _service.ListAsync(_agencyId, null, InquiryStatus.Spam, null, new DateOnly(2026, 10, 26), new DateOnly(2026, 10, 26), 1, 50, CancellationToken.None);

        Assert.Equal([first.SubmissionId], open.Items.Select(i => (Guid?)i.Id));
        Assert.Equal([second.SubmissionId], spam.Items.Select(i => (Guid?)i.Id));
        Assert.Single(byName.Items);
        Assert.Single(today.Items);
        await Assert.ThrowsAsync<WorkspaceNotFoundException>(() => _service.GetAsync(first.SubmissionId.Value, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public void Placeholders_in_auto_replies_are_case_insensitive_and_unknown_ones_vanish()
    {
        var fields = new Dictionary<string, string> { ["Name"] = "Ana", ["dest"] = "Crete" };
        Assert.Equal("Hi Ana, Crete ", InquiryFormService.Fill("Hi {{name}}, {{ DEST }} {{nothing}}", fields));
    }
}
