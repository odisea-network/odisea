using Odisea.SharedKernel;

namespace Odisea.Modules.Documents.Domain;

/// The issuing company's own requisites (доставчик). Single row, operator-managed.
public class CompanyProfile : Entity
{
    public required string Name { get; set; }
    public required string Eik { get; set; }
    public string? VatNumber { get; set; }
    public required string Address { get; set; }
    public required string City { get; set; }
    public required string Mol { get; set; }
    public string? Iban { get; set; }
    public string? BankName { get; set; }
}
