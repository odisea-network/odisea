using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.Infrastructure;
using Odisea.Modules.Agencies.PublicApi;

namespace Odisea.Modules.Agencies.Features.Lookup;

public class AgencyLookup(AgenciesDbContext db) : IAgencyLookup
{
    public async Task<AgencySnapshot?> FindAsync(Guid agencyId, CancellationToken ct)
    {
        var agency = await db.Agencies.AsNoTracking().FirstOrDefaultAsync(a => a.Id == agencyId, ct);
        return agency is null
            ? null
            : new AgencySnapshot(agency.Id, agency.Name, agency.Status == AgencyStatus.Active, agency.CreditLimit);
    }
}
