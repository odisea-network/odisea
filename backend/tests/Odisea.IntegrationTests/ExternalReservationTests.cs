using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.Features.Plans;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Workspace.Domain;
using Odisea.Modules.Workspace.Features.ExternalReservations;

namespace Odisea.IntegrationTests;

[Collection("api")]
public class ExternalReservationTests(OdiseaApiFactory factory)
{
    private const string Base = "/api/v1/agency/external-reservations";

    private static CreateExternalReservationRequest Request(string reference) => new(
        reference, "Own hotel, Antalya", null,
        new CustomerRequest("Georgi Ivanov", "georgi@example.com", "+359888000111"),
        new DateOnly(2027, 5, 1), new DateOnly(2027, 5, 8), ["website"],
        JsonDocument.Parse("""{"hotel":"Sunset Resort","rooms":2,"extras":["transfer"]}""").RootElement.Clone(),
        "Booked on the website");

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var dto = await response.Content.ReadFromJsonAsync<T>(Json.Options);
        Assert.NotNull(dto);
        return dto;
    }

    [Fact]
    public async Task Agency_records_tracks_and_archives_a_reservation_and_the_customer_follows_it_anonymously()
    {
        var agent = factory.ClientAs(TestIdentity.Agent);
        var anonymous = factory.CreateClient();
        var reference = $"WEB-{Guid.NewGuid():N}"[..20];

        var create = await agent.PostAsJsonAsync(Base, Request(reference), Json.Options);
        await create.ShouldBeAsync(HttpStatusCode.Created);
        var dto = await ReadAsync<ExternalReservationDto>(create);
        Assert.Equal(TrackingCode.Length, dto.TrackingCode.Length);
        Assert.Equal("New", dto.Status);
        Assert.Equal("Sunset Resort", dto.Payload.GetProperty("hotel").GetString());
        Assert.Equal(ExternalEventKind.Created, Assert.Single(dto.Events).Kind);
        Assert.Equal(WorkspaceActor.Agency, dto.Events[0].ActorType);

        await (await agent.PostAsJsonAsync(Base, Request(reference), Json.Options)).ShouldBeAsync(HttpStatusCode.Conflict);

        // Status change with an internal note, then a public note.
        await (await agent.PostAsJsonAsync($"{Base}/{dto.Id}/status", new ChangeStatusRequest("Deposit paid", "internal: 50% in", false), Json.Options))
            .ShouldBeAsync(HttpStatusCode.OK);
        await (await agent.PostAsJsonAsync($"{Base}/{dto.Id}/notes", new AddNoteRequest("Vouchers will follow by e-mail", true), Json.Options))
            .ShouldBeAsync(HttpStatusCode.OK);

        // The customer's page: anonymous, status + public timeline, nothing personal.
        var track = await anonymous.GetAsync($"/api/v1/public/track/{dto.TrackingCode.ToLowerInvariant()}");
        await track.ShouldBeAsync(HttpStatusCode.OK);
        Assert.Equal("no-store", track.Headers.CacheControl?.ToString());
        var json = JsonDocument.Parse(await track.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(reference, json.GetProperty("reference").GetString());
        Assert.Equal("Deposit paid", json.GetProperty("status").GetString());
        Assert.Equal("Blue Horizon Travel", json.GetProperty("agencyName").GetString());
        Assert.False(json.TryGetProperty("customer", out _));
        Assert.False(json.TryGetProperty("payload", out _));
        Assert.DoesNotContain("georgi", json.GetRawText(), StringComparison.OrdinalIgnoreCase);
        var events = json.GetProperty("events").EnumerateArray().ToList();
        Assert.Equal(3, events.Count);
        Assert.Equal(JsonValueKind.Null, events[1].GetProperty("note").ValueKind);
        Assert.Equal("Vouchers will follow by e-mail", events[2].GetProperty("note").GetString());

        await (await anonymous.GetAsync($"/api/v1/public/track/{TrackingCode.New()}")).ShouldBeAsync(HttpStatusCode.NotFound);
        await (await anonymous.GetAsync("/api/v1/public/track/not-a-code")).ShouldBeAsync(HttpStatusCode.NotFound);

        // Lists and filters.
        var byStatus = await ReadAsync<PagedResult<ExternalReservationSummaryDto>>(await agent.GetAsync($"{Base}?status=Deposit%20paid&q={reference[..8]}"));
        Assert.Contains(byStatus.Items, i => i.Id == dto.Id);
        var statuses = await ReadAsync<List<string>>(await agent.GetAsync($"{Base}/statuses"));
        Assert.Contains("Deposit paid", statuses);

        // Archive: gone from the default list and from the public page.
        await (await agent.PostAsync($"{Base}/{dto.Id}/archive", null)).ShouldBeAsync(HttpStatusCode.OK);
        var active = await ReadAsync<PagedResult<ExternalReservationSummaryDto>>(await agent.GetAsync($"{Base}?q={reference}"));
        Assert.DoesNotContain(active.Items, i => i.Id == dto.Id);
        await (await anonymous.GetAsync($"/api/v1/public/track/{dto.TrackingCode}")).ShouldBeAsync(HttpStatusCode.NotFound);
        await (await agent.PostAsync($"{Base}/{dto.Id}/unarchive", null)).ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Writes_need_the_workspace_plan_and_operators_see_usage()
    {
        var admin = factory.ClientAs(TestIdentity.Admin);
        var agent = factory.ClientAs(TestIdentity.Agent);
        var plan = await agent.GetFromJsonAsync<AgencyPlanDto>("/api/v1/agency/plan", Json.Options);
        Assert.NotNull(plan);

        await (await agent.PostAsJsonAsync(Base, Request($"PLAN-{Guid.NewGuid():N}"[..20]), Json.Options)).ShouldBeAsync(HttpStatusCode.Created);

        try
        {
            await (await admin.PutAsJsonAsync($"/api/v1/agencies/{plan.AgencyId}/plan", new SetAgencyPlanRequest(AgencyPlan.Core), Json.Options))
                .ShouldBeAsync(HttpStatusCode.OK);

            var denied = await agent.PostAsJsonAsync(Base, Request($"DENIED-{Guid.NewGuid():N}"[..20]), Json.Options);
            await denied.ShouldBeAsync(HttpStatusCode.Forbidden);
            var problem = JsonDocument.Parse(await denied.Content.ReadAsStringAsync()).RootElement;
            Assert.Equal(ProblemTypes.FeatureNotInPlan, problem.GetProperty("type").GetString());

            // Reads keep working after a downgrade.
            await (await agent.GetAsync(Base)).ShouldBeAsync(HttpStatusCode.OK);
        }
        finally
        {
            await (await admin.PutAsJsonAsync($"/api/v1/agencies/{plan.AgencyId}/plan", new SetAgencyPlanRequest(AgencyPlan.Workspace), Json.Options))
                .ShouldBeAsync(HttpStatusCode.OK);
        }

        var usage = await ReadAsync<List<WorkspaceUsageDto>>(await admin.GetAsync("/api/v1/admin/workspace/usage"));
        Assert.Contains(usage, u => u.AgencyId == plan.AgencyId && u.ExternalReservations >= 1);
        await (await admin.GetAsync("/api/v1/admin/workspace/usage?month=nope")).ShouldBeAsync(HttpStatusCode.UnprocessableEntity);
        await (await agent.GetAsync("/api/v1/admin/workspace/usage")).ShouldBeAsync(HttpStatusCode.Forbidden);
        await (await admin.GetAsync(Base)).ShouldBeAsync(HttpStatusCode.Forbidden);
        var oversight = await ReadAsync<PagedResult<ExternalReservationSummaryDto>>(
            await admin.GetAsync($"/api/v1/admin/workspace/external-reservations?agencyId={plan.AgencyId}"));
        Assert.NotEmpty(oversight.Items);
    }
}
