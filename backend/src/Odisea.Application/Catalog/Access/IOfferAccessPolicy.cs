using Odisea.Domain.Entities;

namespace Odisea.Application.Catalog.Access;

// Decides which offers an agency is allowed to distribute. Applied before a
// Collection's FilterSpec runs, so an agency can only ever resolve:
//   - its own AgencyPrivate offers, and
//   - PlatformShared offers from operators it holds an Active entitlement with.
// This is what makes OperatorAgencyEntitlement (issue #12) actually gate access
// rather than just record commercial terms.
public interface IOfferAccessPolicy
{
    Task<IQueryable<Offer>> DistributableForAgencyAsync(Guid agencyId, CancellationToken ct);
}
