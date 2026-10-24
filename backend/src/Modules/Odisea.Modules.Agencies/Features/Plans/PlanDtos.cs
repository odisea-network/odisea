using System.ComponentModel.DataAnnotations;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.PublicApi;

namespace Odisea.Modules.Agencies.Features.Plans;

public sealed record AgencyPlanDto(Guid AgencyId, AgencyPlan Plan, bool IsActive, AgencyFeature[] Features);

public sealed record SetAgencyPlanRequest([Required] AgencyPlan Plan);

public static class PlanDtoMapping
{
    public static AgencyPlanDto ToPlanDto(this Agency agency) =>
        new(agency.Id, agency.Plan, agency.Status == AgencyStatus.Active, [.. PlanFeatures.For(agency.Plan)]);
}
