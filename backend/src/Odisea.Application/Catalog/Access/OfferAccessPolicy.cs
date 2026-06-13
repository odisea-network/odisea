using Microsoft.EntityFrameworkCore;
using Odisea.Application.Common.Interfaces;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;

namespace Odisea.Application.Catalog.Access;

public sealed class OfferAccessPolicy(IAppDbContext db) : IOfferAccessPolicy
{
    public async Task<IQueryable<Offer>> DistributableForAgencyAsync(Guid agencyId, CancellationToken ct)
    {
        // Operators this agency may currently distribute. Suspended entitlements
        // drop out here, so suspending one immediately narrows the agency's catalog.
        var entitledOperatorIds = await db.Entitlements
            .Where(e => e.AgencyId == agencyId && e.Status == EntitlementStatus.Active)
            .Select(e => e.OperatorId)
            .ToListAsync(ct);

        return db.Offers.Where(o =>
            // The agency's own private offers are always its to distribute.
            (o.Visibility == Visibility.AgencyPrivate && o.OwningAgencyId == agencyId)
            // Operator offers require an Active entitlement with that operator.
            || (o.Visibility == Visibility.PlatformShared
                && o.OwningOperatorId != null
                && entitledOperatorIds.Contains(o.OwningOperatorId.Value)));
    }
}
