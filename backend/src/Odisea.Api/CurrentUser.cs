using Odisea.Modules.Agencies.PublicApi;
using Odisea.SharedKernel;

namespace Odisea.Api;

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private System.Security.Claims.ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirst("sub")?.Value, out var id) ? id : null;

    public Guid? AgencyId =>
        Guid.TryParse(Principal?.FindFirst(AuthClaims.AgencyId)?.Value, out var id) ? id : null;

    public string? Role => Principal?.FindFirst(AuthClaims.Role)?.Value;
}
