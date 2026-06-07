namespace Odisea.Application.Auth.Dtos;

public record RegisterRequest(
    string Email,
    string Password,
    string DisplayName,
    Guid? AgencyId,
    Guid? OperatorId
);

public record LoginRequest(
    string Email,
    string Password
);

public record RefreshRequest(
    string RefreshToken
);

public record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt
);

public record CurrentUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    string? TenantType,
    Guid? TenantId
);
