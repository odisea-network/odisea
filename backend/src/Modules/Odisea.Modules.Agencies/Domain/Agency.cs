using Odisea.SharedKernel;

namespace Odisea.Modules.Agencies.Domain;

public class Agency : Entity
{
    public required string Name { get; set; }

    /// ISO 3166-1 alpha-2, e.g. "BG".
    public required string Country { get; set; }

    public AgencyStatus Status { get; set; } = AgencyStatus.Active;

    /// EUR — the platform's base currency.
    public decimal CreditLimit { get; set; }

    public string? ContactEmail { get; set; }
    public Guid? CommissionLevelId { get; set; }

    // Billing requisites for tax documents (чл.114 ЗДДС): the registered
    // company name, ЕИК/UIC, optional ИН по ЗДДС, seat address and МОЛ.
    public string? LegalName { get; set; }
    public string? Eik { get; set; }
    public string? VatNumber { get; set; }
    public string? BillingAddress { get; set; }
    public string? BillingCity { get; set; }
    public string? Mol { get; set; }
}

public enum AgencyStatus
{
    Active,
    Suspended,
}
