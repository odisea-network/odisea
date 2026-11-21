using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Workspace.Domain;
using Odisea.Modules.Workspace.Domain.Ledger;
using Odisea.SharedKernel;

namespace Odisea.Modules.Workspace.Controllers;

/// Shared by the ledger controllers: the agency always comes from the token, and every rejection is
/// a ProblemDetails with a stable "code" extension the portal maps to its own wording.
public abstract class LedgerControllerBase(ICurrentUser currentUser) : ControllerBase
{
    protected async Task<IActionResult> Execute(Func<Guid, LedgerActor, Task<IActionResult>> action)
    {
        if (currentUser.AgencyId is not { } agencyId || currentUser.UserId is not { } userId)
            return Problem(title: "No agency context", statusCode: StatusCodes.Status403Forbidden);

        var actor = new LedgerActor(
            userId,
            currentUser.IsApiClient ? WorkspaceActor.ApiClient : WorkspaceActor.Agency,
            IsAdmin: !currentUser.IsApiClient && currentUser.Role == AuthClaims.RoleAgencyAdmin);
        try
        {
            return await action(agencyId, actor);
        }
        catch (LedgerNotFoundException)
        {
            return NotFound();
        }
        catch (LedgerValidationException ex)
        {
            return Coded(StatusCodes.Status422UnprocessableEntity, "Invalid ledger entry", ex.Message, ex.Code);
        }
        catch (LedgerConflictException ex)
        {
            return Coded(StatusCodes.Status409Conflict, "Conflict", ex.Message, ex.Code);
        }
        catch (LedgerAdminRequiredException ex)
        {
            return Coded(StatusCodes.Status403Forbidden, "Agency admin required", ex.Message, "admin_required");
        }
        catch (FeatureNotInPlanException ex)
        {
            return Problem(type: ProblemTypes.FeatureNotInPlan, title: "Not included in the agency's plan",
                detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (AgencySuspendedException ex)
        {
            return Problem(type: ProblemTypes.AgencySuspended, title: "Agency suspended",
                detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Coded(StatusCodes.Status409Conflict, "Changed by someone else",
                "The reservation was changed in the meantime. Reload it and try again.", "stale_version");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Coded(StatusCodes.Status409Conflict, "Duplicate", "The same entry was saved a moment ago.", "duplicate");
        }
    }

    private ObjectResult Coded(int status, string title, string detail, string code) =>
        Problem(detail: detail, statusCode: status, title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}
