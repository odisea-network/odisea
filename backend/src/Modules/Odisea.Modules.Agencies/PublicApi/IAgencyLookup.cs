namespace Odisea.Modules.Agencies.PublicApi;

/// Read-only agency facts other modules may depend on.
public interface IAgencyLookup
{
    Task<AgencySnapshot?> FindAsync(Guid agencyId, CancellationToken ct);

    /// Billing requisites for tax documents; null fields mean the agency
    /// is not yet invoiceable (чл.114 ЗДДС needs name, ЕИК and address).
    Task<AgencyBillingInfo?> GetBillingInfoAsync(Guid agencyId, CancellationToken ct);
}

public sealed record AgencySnapshot(Guid Id, string Name, bool IsActive, decimal CreditLimit);

public sealed record AgencyBillingInfo(
    Guid AgencyId,
    string? LegalName,
    string? Eik,
    string? VatNumber,
    string? Address,
    string? City,
    string? Mol);
