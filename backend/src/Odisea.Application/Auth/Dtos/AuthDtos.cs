namespace Odisea.Application.Auth.Dtos;

public record RegisterRequest(
    string Email,
    string Password,
    string DisplayName,
    Guid? AgencyId,
    Guid? OperatorId,
    // Self-serve signup: instead of joining an existing tenant by id, a new user
    // can name their own agency/operator. TenantRole is "agency" | "operator" | "both".
    string? TenantName = null,
    string? TenantRole = null
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
