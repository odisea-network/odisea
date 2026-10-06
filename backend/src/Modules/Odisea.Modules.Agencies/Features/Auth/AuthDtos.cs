using System.ComponentModel.DataAnnotations;

namespace Odisea.Modules.Agencies.Features.Auth;

public record LoginRequest(
    [Required, EmailAddress, MaxLength(320)] string Email,
    [Required, MaxLength(128)] string Password);

public record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string UserType,
    string Role,
    Guid? AgencyId,
    string FullName);
