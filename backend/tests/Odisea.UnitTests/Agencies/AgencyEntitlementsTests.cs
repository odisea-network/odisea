using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.Features.Plans;
using Odisea.Modules.Agencies.Infrastructure;
using Odisea.Modules.Agencies.PublicApi;
using Xunit;

namespace Odisea.UnitTests.Agencies;

public class AgencyEntitlementsTests
{
    private readonly AgenciesDbContext _db = new(new DbContextOptionsBuilder<AgenciesDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private async Task<Agency> AgencyAsync(AgencyPlan plan, AgencyStatus status = AgencyStatus.Active)
    {
        var agency = new Agency { Name = "A", Country = "BG", Plan = plan, Status = status };
        _db.Agencies.Add(agency);
        await _db.SaveChangesAsync();
        return agency;
    }

    [Fact]
    public void Core_has_no_workspace_features_and_workspace_has_all_of_them()
    {
        Assert.Empty(PlanFeatures.For(AgencyPlan.Core));
        Assert.Equal(Enum.GetValues<AgencyFeature>().OrderBy(f => f), PlanFeatures.For(AgencyPlan.Workspace).OrderBy(f => f));
    }

    [Fact]
    public async Task New_agencies_start_on_core()
    {
        var agency = await AgencyAsync(default);
        Assert.Equal(AgencyPlan.Core, agency.Plan);
    }

    [Fact]
    public async Task Get_reports_plan_status_and_features()
    {
        var service = new AgencyEntitlementsService(_db);
        var workspace = await AgencyAsync(AgencyPlan.Workspace);
        var core = await AgencyAsync(AgencyPlan.Core, AgencyStatus.Suspended);

        var w = await service.GetAsync(workspace.Id, CancellationToken.None);
        var c = await service.GetAsync(core.Id, CancellationToken.None);

        Assert.NotNull(w);
        Assert.Equal("Workspace", w.Plan);
        Assert.True(w.IsActive);
        Assert.True(w.Has(AgencyFeature.InquiryForms));
        Assert.NotNull(c);
        Assert.False(c.IsActive);
        Assert.False(c.Has(AgencyFeature.InquiryForms));
        Assert.Null(await service.GetAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Require_passes_for_entitled_active_agencies_and_names_the_plan_otherwise()
    {
        var service = new AgencyEntitlementsService(_db);
        var workspace = await AgencyAsync(AgencyPlan.Workspace);
        var core = await AgencyAsync(AgencyPlan.Core);
        var suspended = await AgencyAsync(AgencyPlan.Workspace, AgencyStatus.Suspended);

        await service.RequireAsync(workspace.Id, AgencyFeature.ExternalReservations, CancellationToken.None);

        var denied = await Assert.ThrowsAsync<FeatureNotInPlanException>(() =>
            service.RequireAsync(core.Id, AgencyFeature.ExternalReservations, CancellationToken.None));
        Assert.Equal(AgencyFeature.ExternalReservations, denied.Feature);
        Assert.Equal("Core", denied.Plan);

        await Assert.ThrowsAsync<AgencySuspendedException>(() =>
            service.RequireAsync(suspended.Id, AgencyFeature.ExternalReservations, CancellationToken.None));
        var unknown = await Assert.ThrowsAsync<FeatureNotInPlanException>(() =>
            service.RequireAsync(Guid.NewGuid(), AgencyFeature.InquiryForms, CancellationToken.None));
        Assert.Equal("none", unknown.Plan);
    }
}
