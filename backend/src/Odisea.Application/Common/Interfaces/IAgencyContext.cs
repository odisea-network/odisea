namespace Odisea.Application.Common.Interfaces;

public interface IAgencyContext
{
    bool HasAgency { get; }

    // Null for callers without an agency tenant (e.g. PlatformAdmin, who carries no
    // tenantId claim). Agency-scoped reads filter on it; agency-scoped writes require it.
    Guid? AgencyId { get; }
}
