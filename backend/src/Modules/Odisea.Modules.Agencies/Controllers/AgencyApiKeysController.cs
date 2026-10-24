using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.Features.ApiKeys;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.SharedKernel;

namespace Odisea.Modules.Agencies.Controllers;

/// Operators issue and revoke the machine credentials of external agencies.
[ApiController]
[Route("api/v1/agencies/{agencyId:guid}/api-keys")]
[Authorize(Policy = AuthPolicies.Operator)]
public class AgencyApiKeysController(ApiKeyService keys, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ApiKeyDto>>> List(Guid agencyId, CancellationToken ct) =>
        await keys.ListAsync(agencyId, ct) is { } list ? Ok(list) : NotFound();

    /// The response is the only time the secret is ever shown.
    [HttpPost]
    public async Task<ActionResult<ApiKeyCreatedDto>> Create(
        Guid agencyId, CreateApiKeyRequest request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } operatorId)
            return Problem(title: "No operator context", statusCode: StatusCodes.Status403Forbidden);

        try
        {
            var created = await keys.CreateAsync(agencyId, request, operatorId, ct);
            return created is null
                ? NotFound()
                : CreatedAtAction(nameof(List), new { agencyId }, created);
        }
        catch (ApiKeyValidationException ex)
        {
            return Problem(title: "Invalid API key request", detail: ex.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    [HttpDelete("{keyId:guid}")]
    public async Task<IActionResult> Revoke(Guid agencyId, Guid keyId, CancellationToken ct) =>
        await keys.RevokeAsync(agencyId, keyId, ct) ? NoContent() : NotFound();
}
