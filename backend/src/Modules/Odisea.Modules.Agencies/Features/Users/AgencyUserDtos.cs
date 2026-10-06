using System.ComponentModel.DataAnnotations;
using Odisea.Modules.Agencies.Domain;

namespace Odisea.Modules.Agencies.Features.Users;

public record AgencyUserDto(
    Guid Id,
    Guid AgencyId,
    string Email,
    string FullName,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAt);

public record CreateAgencyUserRequest(
    [Required, EmailAddress, MaxLength(320)] string Email,
    [Required, MaxLength(200)] string FullName,
    [Required] AgencyUserRole Role,
    // NIST: length over composition rules.
    [Required, MinLength(12), MaxLength(128)] string Password);

public static class AgencyUserDtoMapping
{
    public static AgencyUserDto ToDto(this AgencyUser user) => new(
        user.Id,
        user.AgencyId,
        user.Email,
        user.FullName,
        user.Role.ToString(),
        user.IsActive,
        user.CreatedAt);
}
