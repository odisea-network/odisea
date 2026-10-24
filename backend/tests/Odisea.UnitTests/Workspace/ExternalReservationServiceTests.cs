using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Workspace.Domain;
using Odisea.Modules.Workspace.Features.ExternalReservations;
using Odisea.Modules.Workspace.Features.Usage;
using Odisea.Modules.Workspace.Infrastructure;
using Odisea.SharedKernel;
using Xunit;

namespace Odisea.UnitTests.Workspace;

public class ExternalReservationServiceTests
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

    private sealed class FakeAgencyLookup : IAgencyLookup
    {
        public Task<AgencySnapshot?> FindAsync(Guid agencyId, CancellationToken ct) =>
            Task.FromResult<AgencySnapshot?>(new AgencySnapshot(agencyId, "Blue Horizon Travel", true, 0m));
        public Task<AgencyBillingInfo?> GetBillingInfoAsync(Guid agencyId, CancellationToken ct) =>
            Task.FromResult<AgencyBillingInfo?>(null);
        public Task<AgencyContractInfo?> GetContractInfoAsync(Guid agencyId, CancellationToken ct) =>
            Task.FromResult<AgencyContractInfo?>(null);
    }

    private readonly WorkspaceDbContext _db;
    private readonly FakeClock _clock = new();
    private readonly FakeEntitlements _entitlements = new();
    private readonly ExternalReservationService _service;
    private readonly Guid _agencyId = Guid.NewGuid();
    private readonly Guid _otherAgencyId = Guid.NewGuid();
    private readonly WorkspaceActor _agent = new(WorkspaceActor.Agency, Guid.NewGuid());
    private readonly WorkspaceActor _key = new(WorkspaceActor.ApiClient, Guid.NewGuid());

    public ExternalReservationServiceTests()
    {
        _db = new WorkspaceDbContext(new DbContextOptionsBuilder<WorkspaceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _service = new ExternalReservationService(_db, _entitlements, new FakeAgencyLookup(), _clock,
            NullLogger<ExternalReservationService>.Instance);
        _entitlements.Workspace.Add(_agencyId);
        _entitlements.Workspace.Add(_otherAgencyId);
    }

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private Task<ExternalReservationDto> CreateAsync(string reference = "BH-1001", string? status = null, string? payload = null,
        string[]? tags = null, CustomerRequest? customer = null, Guid? agencyId = null, WorkspaceActor? actor = null) =>
        _service.CreateAsync(agencyId ?? _agencyId, actor ?? _agent, new CreateExternalReservationRequest(
            reference, "Antalya, own hotel", status,
            customer ?? new CustomerRequest("Georgi Ivanov", "Georgi@Example.com", "+359888000111"),
            new DateOnly(2027, 5, 1), new DateOnly(2027, 5, 8), tags,
            Json(payload ?? """{"hotel":"Sunset Resort","rooms":[{"type":"DBL","pax":2}],"note":"anything"}"""), "Created from the website"),
            CancellationToken.None);

    [Fact]
    public async Task Create_stores_the_payload_verbatim_with_a_tracking_code_and_a_created_event()
    {
        var dto = await CreateAsync(tags: [" VIP", "early-bird", "vip"], actor: _key);

        Assert.Equal("BH-1001", dto.Reference);
        Assert.Equal(TrackingCode.Length, dto.TrackingCode.Length);
        Assert.Equal(ExternalReservation.DefaultStatus, dto.Status);
        Assert.Equal("georgi@example.com", dto.Customer.Email);
        Assert.Equal(["vip", "early-bird"], dto.Tags);
        Assert.Equal("Sunset Resort", dto.Payload.GetProperty("hotel").GetString());
        Assert.Equal(2, dto.Payload.GetProperty("rooms")[0].GetProperty("pax").GetInt32());
        var created = Assert.Single(dto.Events);
        Assert.Equal(ExternalEventKind.Created, created.Kind);
        Assert.Equal("New", created.ToStatus);
        Assert.Equal("Created from the website", created.Note);
        Assert.Equal(WorkspaceActor.ApiClient, created.ActorType);
        Assert.Equal(_clock.UtcNow, dto.UpdatedAt);
    }

    [Fact]
    public async Task Create_is_gated_by_the_plan_and_by_suspension()
    {
        var core = Guid.NewGuid();
        await Assert.ThrowsAsync<FeatureNotInPlanException>(() => CreateAsync(agencyId: core));
        _entitlements.Suspended.Add(_agencyId);
        await Assert.ThrowsAsync<AgencySuspendedException>(() => CreateAsync());
        Assert.Empty(_db.ExternalReservations);
    }

    [Fact]
    public async Task References_are_unique_per_agency_only()
    {
        await CreateAsync();
        await Assert.ThrowsAsync<DuplicateReferenceException>(() => CreateAsync());
        await CreateAsync(agencyId: _otherAgencyId);
        Assert.Equal(2, await _db.ExternalReservations.CountAsync());
    }

    [Fact]
    public async Task Validation_rejects_oversized_payloads_bad_tags_and_inverted_dates()
    {
        var big = "{\"blob\":\"" + new string('x', ExternalReservation.MaxPayloadBytes) + "\"}";
        await Assert.ThrowsAsync<WorkspaceValidationException>(() => CreateAsync(payload: big));
        await Assert.ThrowsAsync<WorkspaceValidationException>(() => CreateAsync(tags: Enumerable.Range(0, 11).Select(i => $"t{i}").ToArray()));
        await Assert.ThrowsAsync<WorkspaceValidationException>(() => CreateAsync(tags: [new string('a', 31)]));
        await Assert.ThrowsAsync<WorkspaceValidationException>(() => _service.CreateAsync(_agencyId, _agent,
            new CreateExternalReservationRequest("X", null, null, null, new DateOnly(2027, 5, 8), new DateOnly(2027, 5, 1), null, null, null),
            CancellationToken.None));
        Assert.Equal("{}", ExternalReservationService.NormalizePayload(null));
    }

    [Fact]
    public async Task Status_changes_notes_and_updates_build_the_timeline_and_touch_updated_at()
    {
        var dto = await CreateAsync();
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        await _service.ChangeStatusAsync(dto.Id, _agencyId, _agent, new ChangeStatusRequest("Deposit paid", "50% received", IsPublic: true), CancellationToken.None);
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        await _service.AddNoteAsync(dto.Id, _agencyId, _agent, new AddNoteRequest("Call the hotel", IsPublic: false), CancellationToken.None);
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        var updated = await _service.UpdateAsync(dto.Id, _agencyId, _key, new UpdateExternalReservationRequest(
            "Antalya, own hotel (changed)", null, null, null, ["vip"], Json("[1,2,3]"), "payload replaced"), CancellationToken.None);

        Assert.Equal("Deposit paid", updated.Status);
        Assert.Equal("Antalya, own hotel (changed)", updated.Title);
        Assert.Null(updated.Customer.Name);
        Assert.Equal(JsonValueKind.Array, updated.Payload.ValueKind);
        Assert.Equal(_clock.UtcNow, updated.UpdatedAt);
        Assert.Equal(
            [ExternalEventKind.Created, ExternalEventKind.StatusChanged, ExternalEventKind.Note, ExternalEventKind.Updated],
            updated.Events.Select(e => e.Kind));
        var change = updated.Events[1];
        Assert.Equal(("New", "Deposit paid", true), (change.FromStatus, change.ToStatus, change.IsPublic));
    }

    [Fact]
    public async Task Public_tracking_shows_status_history_and_public_notes_only()
    {
        var dto = await CreateAsync();
        await _service.ChangeStatusAsync(dto.Id, _agencyId, _agent, new ChangeStatusRequest("Confirmed", "internal remark", IsPublic: false), CancellationToken.None);
        await _service.AddNoteAsync(dto.Id, _agencyId, _agent, new AddNoteRequest("Vouchers sent by e-mail", IsPublic: true), CancellationToken.None);
        await _service.AddNoteAsync(dto.Id, _agencyId, _agent, new AddNoteRequest("Margin 12%", IsPublic: false), CancellationToken.None);

        var page = await _service.TrackAsync(dto.TrackingCode.ToLowerInvariant().Insert(5, "-"), CancellationToken.None);

        Assert.Equal("BH-1001", page.Reference);
        Assert.Equal("Confirmed", page.Status);
        Assert.Equal("Blue Horizon Travel", page.AgencyName);
        Assert.Equal([ExternalEventKind.Created, ExternalEventKind.StatusChanged, ExternalEventKind.Note], page.Events.Select(e => e.Kind));
        Assert.Null(page.Events[1].Note);
        Assert.Equal("Vouchers sent by e-mail", page.Events[2].Note);
        await Assert.ThrowsAsync<WorkspaceNotFoundException>(() => _service.TrackAsync("NOPE", CancellationToken.None));
        await Assert.ThrowsAsync<WorkspaceNotFoundException>(() => _service.TrackAsync(TrackingCode.New(), CancellationToken.None));
    }

    [Fact]
    public async Task Archiving_hides_from_the_default_list_and_from_the_public_page()
    {
        var dto = await CreateAsync();
        await _service.SetArchivedAsync(dto.Id, _agencyId, _agent, archived: true, CancellationToken.None);

        Assert.Empty((await _service.ListAsync(_agencyId, null, null, null, null, null, false, 1, 50, CancellationToken.None)).Items);
        Assert.Single((await _service.ListAsync(_agencyId, null, null, null, null, null, true, 1, 50, CancellationToken.None)).Items);
        await Assert.ThrowsAsync<WorkspaceNotFoundException>(() => _service.TrackAsync(dto.TrackingCode, CancellationToken.None));

        var restored = await _service.SetArchivedAsync(dto.Id, _agencyId, _agent, archived: false, CancellationToken.None);
        Assert.False(restored.IsArchived);
        Assert.Equal(ExternalEventKind.Restored, restored.Events[^1].Kind);
    }

    [Fact]
    public async Task List_filters_by_status_search_tag_and_dates_and_is_scoped_to_the_agency()
    {
        await CreateAsync("BH-1", status: "Confirmed", tags: ["vip"]);
        await CreateAsync("BH-2", status: "New", customer: new CustomerRequest("Maria Petrova", "maria@example.com", null));
        await CreateAsync("OTHER-1", agencyId: _otherAgencyId);

        var all = await _service.ListAsync(_agencyId, null, null, null, null, null, false, 1, 50, CancellationToken.None);
        var confirmed = await _service.ListAsync(_agencyId, "confirmed ", null, null, null, null, false, 1, 50, CancellationToken.None);
        var maria = await _service.ListAsync(_agencyId, null, "MARIA", null, null, null, false, 1, 50, CancellationToken.None);
        var vip = await _service.ListAsync(_agencyId, null, null, null, null, "VIP", false, 1, 50, CancellationToken.None);
        var may = await _service.ListAsync(_agencyId, null, null, new DateOnly(2027, 5, 1), new DateOnly(2027, 5, 31), null, false, 1, 1, CancellationToken.None);
        var june = await _service.ListAsync(_agencyId, null, null, new DateOnly(2027, 6, 1), null, null, false, 1, 50, CancellationToken.None);

        Assert.Equal(2, all.TotalCount);
        Assert.Equal(["BH-1"], confirmed.Items.Select(i => i.Reference));
        Assert.Equal(["BH-2"], maria.Items.Select(i => i.Reference));
        Assert.Equal(["BH-1"], vip.Items.Select(i => i.Reference));
        Assert.Equal((2, 1), (may.TotalCount, may.Items.Count));
        Assert.Empty(june.Items);
        Assert.Equal(["Confirmed", "New"], await _service.StatusesAsync(_agencyId, CancellationToken.None));
        await Assert.ThrowsAsync<WorkspaceNotFoundException>(() => _service.GetAsync(all.Items[0].Id, _otherAgencyId, CancellationToken.None));
    }

    [Fact]
    public async Task Usage_counts_reservations_per_agency_for_the_month()
    {
        await CreateAsync("A-1");
        await CreateAsync("A-2");
        await CreateAsync("B-1", agencyId: _otherAgencyId);
        _clock.UtcNow = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);
        await CreateAsync("A-3");

        var usage = new WorkspaceUsageService(_db, _clock);
        var october = await usage.UsageAsync("2026-10", CancellationToken.None);
        var current = await usage.UsageAsync(null, CancellationToken.None);

        Assert.Equal([(_agencyId, 2), (_otherAgencyId, 1)], october.Select(u => (u.AgencyId, u.ExternalReservations)));
        Assert.All(october, u => Assert.Equal("2026-10", u.Month));
        Assert.Equal([(_agencyId, 1)], current.Select(u => (u.AgencyId, u.ExternalReservations)));
        await Assert.ThrowsAsync<WorkspaceValidationException>(() => usage.UsageAsync("October", CancellationToken.None));
    }

    [Fact]
    public void Tracking_codes_avoid_ambiguous_characters_and_normalize_user_input()
    {
        var code = TrackingCode.New();
        Assert.Equal(10, code.Length);
        Assert.DoesNotContain(code, c => "01OI".Contains(c));
        Assert.Equal(code, TrackingCode.Normalize(code.ToLowerInvariant().Insert(3, " ").Insert(7, "-")));
        Assert.Null(TrackingCode.Normalize("ABCDEFGH10"));
        Assert.Null(TrackingCode.Normalize(null));
    }
}
