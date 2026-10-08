namespace Odisea.Modules.Agencies.PublicApi;

/// Read-only agency facts other modules may depend on.
public interface IAgencyLookup
{
    Task<AgencySnapshot?> FindAsync(Guid agencyId, CancellationToken ct);
}

public sealed record AgencySnapshot(Guid Id, string Name, bool IsActive, decimal CreditLimit);
