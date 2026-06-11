namespace Odisea.Application.Common.Interfaces;

public static class AgencyContextExtensions
{
    // Unwraps the agency id for endpoints that cannot operate without one.
    // Throws when the caller has no agency tenant (e.g. a PlatformAdmin); callers
    // map this to a 400 rather than letting it surface as a 500.
    public static Guid RequireAgency(this IAgencyContext ctx) =>
        ctx.AgencyId ?? throw new InvalidOperationException(
            "Endpoint requires an agency-scoped caller; current user has no tenantId.");
}
