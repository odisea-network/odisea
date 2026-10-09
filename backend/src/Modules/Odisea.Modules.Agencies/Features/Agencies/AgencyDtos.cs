using System.ComponentModel.DataAnnotations;
using Odisea.Modules.Agencies.Domain;

namespace Odisea.Modules.Agencies.Features.Agencies;

public record AgencyDto(
    Guid Id,
    string Name,
    string Country,
    string Status,
    decimal CreditLimit,
    string? ContactEmail,
    Guid? CommissionLevelId,
    AgencyBillingDto? Billing,
    DateTimeOffset CreatedAt);

public record AgencyBillingDto(
    string? LegalName, string? Eik, string? VatNumber,
    string? Address, string? City, string? Mol);

public record UpdateAgencyBillingRequest(
    [Required, MaxLength(200)] string LegalName,
    // ЕИК: 9 digits (13 for branches/ЕТ with extensions).
    [Required, RegularExpression(@"^\d{9}(\d{4})?$")] string Eik,
    // ИН по ЗДДС, only when VAT-registered.
    [RegularExpression(@"^BG\d{9,10}$")] string? VatNumber,
    [Required, MaxLength(300)] string Address,
    [Required, MaxLength(100)] string City,
    [Required, MaxLength(200)] string Mol);

public record CreateAgencyRequest(
    [Required, MaxLength(200)] string Name,
    [Required, RegularExpression("^[A-Za-z]{2}$")] string Country,
    [Range(0, 10_000_000)] decimal CreditLimit,
    [EmailAddress, MaxLength(320)] string? ContactEmail);

public record UpdateAgencyRequest(
    [Required, MaxLength(200)] string Name,
    [Required, RegularExpression("^[A-Za-z]{2}$")] string Country,
    [Range(0, 10_000_000)] decimal CreditLimit,
    [EmailAddress, MaxLength(320)] string? ContactEmail,
    [Required] AgencyStatus Status);

public static class AgencyDtoMapping
{
    public static AgencyDto ToDto(this Agency agency) => new(
        agency.Id,
        agency.Name,
        agency.Country,
        agency.Status.ToString(),
        agency.CreditLimit,
        agency.ContactEmail,
        agency.CommissionLevelId,
        agency.Eik is null ? null : new AgencyBillingDto(
            agency.LegalName, agency.Eik, agency.VatNumber,
            agency.BillingAddress, agency.BillingCity, agency.Mol),
        agency.CreatedAt);
}
