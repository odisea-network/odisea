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
    DateTimeOffset CreatedAt);

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
        agency.CreatedAt);
}
