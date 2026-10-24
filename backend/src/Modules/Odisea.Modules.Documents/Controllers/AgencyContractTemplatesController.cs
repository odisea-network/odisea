using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Documents.Features.Contracts;

namespace Odisea.Modules.Documents.Controllers;

/// The templates an agency may generate itself (active and marked agency-usable by the operator).
[ApiController]
[Route("api/v1/agency/contract-templates")]
[Authorize(Policy = AuthPolicies.Agency)]
public class AgencyContractTemplatesController(ContractService contracts) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ContractTemplateDto>>> List(CancellationToken ct) =>
        Ok(await contracts.ListTemplatesForAgencyAsync(ct));

    /// The placeholders the API fills; the rest of a template's placeholders are custom fields to supply.
    [HttpGet("placeholders")]
    public ActionResult<List<PlaceholderDto>> Placeholders() =>
        Ok(PlaceholderCatalogue.All.Select(p => p.ToDto()).ToList());
}
