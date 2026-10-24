using System.Net;
using System.Net.Http.Json;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.Features.Agencies;
using Odisea.Modules.Agencies.Features.Plans;
using Odisea.Modules.Agencies.PublicApi;

namespace Odisea.IntegrationTests;

[Collection("api")]
public class AgencyPlanTests(OdiseaApiFactory factory)
{
    [Fact]
    public async Task Plan_is_set_by_operator_admins_and_visible_to_the_agency()
    {
        var admin = factory.ClientAs(TestIdentity.Admin);
        var operatorUser = factory.ClientAs(TestIdentity.Operator);
        var agent = factory.ClientAs(TestIdentity.Agent);

        // The demo agency is seeded on Workspace so the dev portal can show the features.
        var mine = await agent.GetFromJsonAsync<AgencyPlanDto>("/api/v1/agency/plan", Json.Options);
        Assert.NotNull(mine);
        Assert.Equal(AgencyPlan.Workspace, mine.Plan);
        Assert.Contains(AgencyFeature.InquiryForms, mine.Features);
        Assert.True(mine.IsActive);

        await (await operatorUser.PutAsJsonAsync($"/api/v1/agencies/{mine.AgencyId}/plan", new SetAgencyPlanRequest(AgencyPlan.Core), Json.Options))
            .ShouldBeAsync(HttpStatusCode.Forbidden);
        await (await agent.PutAsJsonAsync($"/api/v1/agencies/{mine.AgencyId}/plan", new SetAgencyPlanRequest(AgencyPlan.Core), Json.Options))
            .ShouldBeAsync(HttpStatusCode.Forbidden);
        await (await admin.PutAsJsonAsync($"/api/v1/agencies/{Guid.NewGuid()}/plan", new SetAgencyPlanRequest(AgencyPlan.Core), Json.Options))
            .ShouldBeAsync(HttpStatusCode.NotFound);

        try
        {
            var downgraded = await admin.PutAsJsonAsync($"/api/v1/agencies/{mine.AgencyId}/plan", new SetAgencyPlanRequest(AgencyPlan.Core), Json.Options);
            await downgraded.ShouldBeAsync(HttpStatusCode.OK);
            var dto = await downgraded.Content.ReadFromJsonAsync<AgencyDto>(Json.Options);
            Assert.NotNull(dto);
            Assert.Equal("Core", dto.Plan);

            var afterDowngrade = await agent.GetFromJsonAsync<AgencyPlanDto>("/api/v1/agency/plan", Json.Options);
            Assert.NotNull(afterDowngrade);
            Assert.Equal(AgencyPlan.Core, afterDowngrade.Plan);
            Assert.Empty(afterDowngrade.Features);
        }
        finally
        {
            await (await admin.PutAsJsonAsync($"/api/v1/agencies/{mine.AgencyId}/plan", new SetAgencyPlanRequest(AgencyPlan.Workspace), Json.Options))
                .ShouldBeAsync(HttpStatusCode.OK);
        }

        await (await admin.GetAsync("/api/v1/agency/plan")).ShouldBeAsync(HttpStatusCode.Forbidden);
    }
}
