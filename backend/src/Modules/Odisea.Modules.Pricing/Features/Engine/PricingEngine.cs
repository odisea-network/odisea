using Odisea.Modules.Pricing.Domain;
using Odisea.Modules.Pricing.PublicApi;
using Odisea.SharedKernel;

namespace Odisea.Modules.Pricing.Features.Engine;

/// Pure and deterministic: no I/O, no clock, no randomness. Given the same
/// inputs it always produces the same breakdown — the whole engine is
/// exhaustively unit-testable because of this.
public static class PricingEngine
{
    /// <param name="netCost">Net cost already converted to the target currency.</param>
    /// <param name="conversionStep">Optional FX step performed by the caller, recorded for the audit trail.</param>
    /// <param name="candidateRules">Rules whose scope matches the context (Global / this Program / this Agency), unfiltered otherwise.</param>
    public static PriceBreakdown Calculate(
        Money netCost,
        AppliedStep? conversionStep,
        PricingContext context,
        IReadOnlyList<PricingRule> candidateRules)
    {
        var applicable = candidateRules.Where(r => r.AppliesOn(context.TravelDate)).ToList();

        List<AppliedStep> steps = conversionStep is null ? [] : [conversionStep];
        var price = netCost;

        foreach (var kind in (RuleKind[])[RuleKind.Markup, RuleKind.Fee])
        {
            foreach (var rule in WinningScopeRules(applicable, kind))
            {
                var next = Apply(price, rule).Round();
                steps.Add(new AppliedStep(rule.Id, rule.Name, kind.ToString(), price, next));
                price = next;
            }
        }

        var commission = Money.Zero(price.Currency);
        foreach (var rule in WinningScopeRules(applicable, RuleKind.Commission))
        {
            var amount = rule.ValueType == RuleValueType.Percent
                ? (price * (rule.Value / 100m)).Round()
                : new Money(rule.Value, price.Currency);
            var next = (commission + amount).Round();
            steps.Add(new AppliedStep(rule.Id, rule.Name, nameof(RuleKind.Commission), commission, next));
            commission = next;
        }

        return new PriceBreakdown(netCost, steps, price, commission);
    }

    /// Most specific scope that has rules of this kind wins outright:
    /// Agency > Program > Global. Within the winner, lower Priority runs first.
    private static IEnumerable<PricingRule> WinningScopeRules(List<PricingRule> rules, RuleKind kind)
    {
        var ofKind = rules.Where(r => r.Kind == kind).ToList();
        foreach (var scope in (RuleScope[])[RuleScope.Agency, RuleScope.Program, RuleScope.Global])
        {
            var inScope = ofKind.Where(r => r.Scope == scope).ToList();
            if (inScope.Count > 0)
                return inScope.OrderBy(r => r.Priority).ThenBy(r => r.Name, StringComparer.Ordinal);
        }
        return [];
    }

    private static Money Apply(Money price, PricingRule rule) =>
        rule.ValueType == RuleValueType.Percent
            ? price * (1 + rule.Value / 100m)
            : price + new Money(rule.Value, price.Currency);
}
