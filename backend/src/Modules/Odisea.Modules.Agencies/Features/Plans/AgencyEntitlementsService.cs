using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.Infrastructure;
using Odisea.Modules.Agencies.PublicApi;

namespace Odisea.Modules.Agencies.Features.Plans;

public sealed class AgencyEntitlementsService(AgenciesDbContext db) : IAgencyEntitlements
{
    public async Task<AgencyEntitlements?> GetAsync(Guid agencyId, CancellationToken ct)
    {
        var agency = await db.Agencies.AsNoTracking()
            .Where(a => a.Id == agencyId)
            .Select(a => new { a.Plan, a.Status })
            .FirstOrDefaultAsync(ct);
        return agency is null
            ? null
            : new AgencyEntitlements(agencyId, agency.Plan.ToString(), agency.Status == AgencyStatus.Active, PlanFeatures.For(agency.Plan));
    }

    public async Task RequireAsync(Guid agencyId, AgencyFeature feature, CancellationToken ct)
    {
        var entitlements = await GetAsync(agencyId, ct)
            ?? throw new FeatureNotInPlanException(feature, "none");
        if (!entitlements.IsActive)
            throw new AgencySuspendedException();
        if (!entitlements.Has(feature))
            throw new FeatureNotInPlanException(feature, entitlements.Plan);
    }
}
