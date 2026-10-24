using System.Net;
using System.Net.Http.Json;
using Odisea.Modules.Agencies.Features.Agencies;
using Odisea.Modules.Agencies.Features.ApiKeys;
using Odisea.Modules.Booking.Features.Bookings;

namespace Odisea.IntegrationTests;

/// External agencies authenticate with X-Api-Key and must land in exactly the same box as a
/// logged-in agency user: their own agency's data, nothing operator-side, no session.
[Collection(ApiCollection.Name)]
public class ApiKeyAuthTests(OdiseaApiFactory factory)
{
    private const string DemoAgencyName = "Blue Horizon Travel";

    private static async Task<Guid> AgencyIdAsync(HttpClient admin, string name)
    {
        var agencies = await admin.GetFromJsonAsync<List<AgencyDto>>("/api/v1/agencies", Json.Options);
        Assert.NotNull(agencies);
        return agencies.Single(a => a.Name == name).Id;
    }

    private static async Task<ApiKeyCreatedDto> IssueKeyAsync(HttpClient admin, Guid agencyId, string name = "Partner integration")
    {
        var response = await admin.PostAsJsonAsync($"/api/v1/agencies/{agencyId}/api-keys",
            new CreateApiKeyRequest(name), Json.Options);
        await response.ShouldBeAsync(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ApiKeyCreatedDto>(Json.Options);
        Assert.NotNull(created);
        Assert.StartsWith("odk_live_", created.Secret);
        return created;
    }

    [Fact]
    public async Task A_valid_key_acts_as_the_agency_and_leaves_an_api_client_trail()
    {
        var admin = factory.ClientAs(TestIdentity.Admin);
        var agencyId = await AgencyIdAsync(admin, DemoAgencyName);
        var key = await IssueKeyAsync(admin, agencyId);
        var partner = factory.ClientWithApiKey(key.Secret);

        await (await partner.GetAsync("/api/v1/catalog/programs")).ShouldBeAsync(HttpStatusCode.OK);
        var draft = await AgencyFlow.CreateDraftAsync(partner);
        Assert.Equal("ApiClient", draft.History[0].Actor);

        var listResponse = await admin.GetAsync($"/api/v1/agencies/{agencyId}/api-keys");
        await listResponse.ShouldBeAsync(HttpStatusCode.OK);
        var body = await listResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(key.Secret, body);
        var listed = await listResponse.Content.ReadFromJsonAsync<List<ApiKeyDto>>(Json.Options);
        Assert.NotNull(listed);
        var ours = listed.Single(k => k.Id == key.Key.Id);
        Assert.NotNull(ours.LastUsedAt);
        Assert.Equal(key.Secret[..16], ours.KeyPrefix);
    }

    [Fact]
    public async Task Unknown_malformed_and_revoked_keys_are_rejected_with_a_challenge()
    {
        var admin = factory.ClientAs(TestIdentity.Admin);
        var agencyId = await AgencyIdAsync(admin, DemoAgencyName);

        var unknown = await factory.ClientWithApiKey("odk_live_" + new string('x', 43)).GetAsync("/api/v1/catalog/programs");
        await unknown.ShouldBeAsync(HttpStatusCode.Unauthorized);
        Assert.Contains(unknown.Headers.WwwAuthenticate, h => h.Scheme == "ApiKey");

        await (await factory.ClientWithApiKey("short").GetAsync("/api/v1/catalog/programs"))
            .ShouldBeAsync(HttpStatusCode.Unauthorized);

        var key = await IssueKeyAsync(admin, agencyId, "to be revoked");
        await (await factory.ClientWithApiKey(key.Secret).GetAsync("/api/v1/catalog/programs")).ShouldBeAsync(HttpStatusCode.OK);

        await (await admin.DeleteAsync($"/api/v1/agencies/{agencyId}/api-keys/{key.Key.Id}")).ShouldBeAsync(HttpStatusCode.NoContent);
        await (await factory.ClientWithApiKey(key.Secret).GetAsync("/api/v1/catalog/programs")).ShouldBeAsync(HttpStatusCode.Unauthorized);
        await (await admin.DeleteAsync($"/api/v1/agencies/{agencyId}/api-keys/{key.Key.Id}")).ShouldBeAsync(HttpStatusCode.NoContent);
        await (await admin.DeleteAsync($"/api/v1/agencies/{agencyId}/api-keys/{Guid.NewGuid()}")).ShouldBeAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Keys_never_reach_operator_endpoints_or_obtain_a_session()
    {
        var admin = factory.ClientAs(TestIdentity.Admin);
        var agencyId = await AgencyIdAsync(admin, DemoAgencyName);
        var partner = factory.ClientWithApiKey((await IssueKeyAsync(admin, agencyId)).Secret);

        await (await partner.GetAsync("/api/v1/admin/bookings")).ShouldBeAsync(HttpStatusCode.Forbidden);
        await (await partner.GetAsync("/api/v1/agencies")).ShouldBeAsync(HttpStatusCode.Forbidden);
        await (await partner.PostAsJsonAsync($"/api/v1/agencies/{agencyId}/api-keys",
            new CreateApiKeyRequest("escalation"), Json.Options)).ShouldBeAsync(HttpStatusCode.Forbidden);
        await (await partner.PostAsync("/api/v1/auth/refresh", null)).ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Keys_are_scoped_to_their_own_agency()
    {
        var admin = factory.ClientAs(TestIdentity.Admin);
        var demoBooking = await AgencyFlow.CreateDraftAsync(factory.ClientAs(TestIdentity.Agent));

        var created = await admin.PostAsJsonAsync("/api/v1/agencies",
            new CreateAgencyRequest("Other Partner Ltd", "BG", 1000m, null), Json.Options);
        await created.ShouldBeAsync(HttpStatusCode.Created);
        var other = await created.Content.ReadFromJsonAsync<AgencyDto>(Json.Options);
        Assert.NotNull(other);
        var partner = factory.ClientWithApiKey((await IssueKeyAsync(admin, other.Id)).Secret);

        await (await partner.GetAsync($"/api/v1/bookings/{demoBooking.Id}")).ShouldBeAsync(HttpStatusCode.NotFound);
        var own = await partner.GetFromJsonAsync<List<BookingDto>>("/api/v1/bookings", Json.Options);
        Assert.NotNull(own);
        Assert.Empty(own);
    }

    [Fact]
    public async Task Agency_users_cannot_manage_keys()
    {
        var admin = factory.ClientAs(TestIdentity.Admin);
        var agencyId = await AgencyIdAsync(admin, DemoAgencyName);

        await (await factory.ClientAs(TestIdentity.Agent).PostAsJsonAsync($"/api/v1/agencies/{agencyId}/api-keys",
            new CreateApiKeyRequest("mine"), Json.Options)).ShouldBeAsync(HttpStatusCode.Forbidden);
        await (await factory.ClientAs(TestIdentity.Agent).GetAsync($"/api/v1/agencies/{agencyId}/api-keys"))
            .ShouldBeAsync(HttpStatusCode.Forbidden);
    }
}
