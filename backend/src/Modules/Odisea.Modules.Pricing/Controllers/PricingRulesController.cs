using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Pricing.Domain;
using Odisea.Modules.Pricing.Features.Rules;
using Odisea.Modules.Pricing.Infrastructure;

namespace Odisea.Modules.Pricing.Controllers;

[ApiController]
[Route("api/v1/pricing-rules")]
[Authorize(Policy = AuthPolicies.Operator)]
public class PricingRulesController(PricingDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PricingRuleDto>>> List(CancellationToken ct) =>
        Ok(await db.PricingRules
            .OrderBy(r => r.Scope).ThenBy(r => r.Kind).ThenBy(r => r.Priority)
            .Select(r => r.ToDto())
            .ToListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<PricingRuleDto>> Create(SavePricingRuleRequest request, CancellationToken ct)
    {
        if (Invalid(request) is { } problem)
            return problem;

        var rule = new PricingRule { Name = request.Name };
        ApplyRequest(rule, request);
        db.PricingRules.Add(rule);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), null, rule.ToDto());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PricingRuleDto>> Update(Guid id, SavePricingRuleRequest request, CancellationToken ct)
    {
        if (Invalid(request) is { } problem)
            return problem;

        var rule = await db.PricingRules.FindAsync([id], ct);
        if (rule is null)
            return NotFound();

        rule.Name = request.Name;
        ApplyRequest(rule, request);
        await db.SaveChangesAsync(ct);
        return Ok(rule.ToDto());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var rule = await db.PricingRules.FindAsync([id], ct);
        if (rule is null)
            return NotFound();

        // Safe to remove outright: every historical price keeps its own breakdown copy.
        db.PricingRules.Remove(rule);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private ObjectResult? Invalid(SavePricingRuleRequest request) =>
        request.Scope != RuleScope.Global && request.ScopeId is null
            ? Problem(title: "Missing scope id",
                detail: $"{request.Scope}-scoped rules need a ScopeId.",
                statusCode: StatusCodes.Status422UnprocessableEntity)
            : null;

    private static void ApplyRequest(PricingRule rule, SavePricingRuleRequest request)
    {
        rule.Scope = request.Scope;
        rule.ScopeId = request.Scope == RuleScope.Global ? null : request.ScopeId;
        rule.Kind = request.Kind;
        rule.ValueType = request.ValueType;
        rule.Value = request.Value;
        rule.Priority = request.Priority;
        rule.ValidFrom = request.ValidFrom;
        rule.ValidTo = request.ValidTo;
        rule.Enabled = request.Enabled;
    }
}
