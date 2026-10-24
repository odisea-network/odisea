using System.Net;

namespace Odisea.IntegrationTests;

[Collection(ApiCollection.Name)]
public class HealthAndAuthSmokeTests(OdiseaApiFactory factory)
{
    [Fact]
    public async Task Health_is_anonymous()
    {
        var response = await factory.AnonymousClient().GetAsync("/health");
        await response.ShouldBeAsync(HttpStatusCode.OK);
        Assert.Contains("healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Anonymous_api_requests_are_rejected()
    {
        var response = await factory.AnonymousClient().GetAsync("/api/v1/bookings");
        await response.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Each_seeded_identity_can_log_in_and_reach_its_own_surface()
    {
        await (await factory.ClientAs(TestIdentity.Admin).GetAsync("/api/v1/agencies"))
            .ShouldBeAsync(HttpStatusCode.OK);
        await (await factory.ClientAs(TestIdentity.Operator).GetAsync("/api/v1/agencies"))
            .ShouldBeAsync(HttpStatusCode.OK);
        await (await factory.ClientAs(TestIdentity.Agent).GetAsync("/api/v1/catalog/programs"))
            .ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Agency_users_cannot_reach_operator_endpoints()
    {
        var response = await factory.ClientAs(TestIdentity.Agent).GetAsync("/api/v1/agencies");
        await response.ShouldBeAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Operators_cannot_reach_agency_endpoints()
    {
        var response = await factory.ClientAs(TestIdentity.Admin).GetAsync("/api/v1/bookings");
        await response.ShouldBeAsync(HttpStatusCode.Forbidden);
    }
}
