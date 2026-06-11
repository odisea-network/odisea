using System.Security.Claims;
using Odisea.Application.Common.Interfaces;
using Odisea.Domain.Enums;

namespace Odisea.WebAPI.Services;

// Scoped per-request implementation of both IUserContext and IAgencyContext.
// Reads parsed JWT claims populated by the JwtBearer middleware.
public class RequestContext(IHttpContextAccessor accessor) : IUserContext, IAgencyContext
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated =>
        Principal?.Identity?.IsAuthenticated == true;

    public Guid UserId =>
        Guid.Parse(Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public string Email =>
        Principal!.FindFirstValue(ClaimTypes.Email)!;

    public UserRole Role =>
        Enum.Parse<UserRole>(Principal!.FindFirstValue(ClaimTypes.Role)!);

    // "Has agency" requires BOTH a tenantType=Agency claim AND a parseable tenantId.
    // A token with the type but no id (e.g. a bad seed or a half-built fixture)
    // would otherwise sneak past this gate and blow up in RequireAgency() — see #43.
    public bool HasAgency =>
        IsAuthenticated &&
        Principal?.FindFirstValue("tenantType") == TenantType.Agency.ToString() &&
        AgencyId is not null;

    public Guid? AgencyId =>
        Guid.TryParse(Principal?.FindFirstValue("tenantId"), out var id) ? id : null;
}
