using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Catalog.Domain;
using Odisea.Modules.Catalog.Features.Search;
using Odisea.SharedKernel;

namespace Odisea.Modules.Catalog.Controllers;

[ApiController]
[Route("api/v1/search")]
[Authorize(Policy = AuthPolicies.Agency)]
public class SearchController(SearchService search, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SearchResponse>> Search(SearchRequest request, CancellationToken ct)
    {
        // Agency identity comes from the JWT claim, never from the request body.
        if (currentUser.AgencyId is not { } agencyId)
            return Problem(title: "No agency context", statusCode: StatusCodes.Status403Forbidden);

        try
        {
            return Ok(await search.SearchAsync(request, agencyId, ct));
        }
        catch (SearchNotFoundException ex)
        {
            return Problem(title: "Not found", detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (MissingMappingException ex)
        {
            return Problem(title: "Configuration incomplete", detail: ex.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }
}
