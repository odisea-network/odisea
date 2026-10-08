using Odisea.Modules.Pricing.Domain;
using Odisea.Modules.Pricing.Features.Engine;
using Odisea.Modules.Pricing.PublicApi;
using Odisea.SharedKernel;
using Xunit;

namespace Odisea.UnitTests.Pricing;

public class PricingEngineTests
{
    private static readonly Guid ProgramId = Guid.NewGuid();
    private static readonly Guid AgencyId = Guid.NewGuid();
    private static readonly DateOnly Travel = new(2027, 3, 30);

    private static PricingContext Context(decimal net = 1000m) =>
        new(Money.Eur(net), ProgramId, AgencyId, Travel);

    private static PricingRule Rule(
        RuleKind kind, decimal value,
        RuleValueType type = RuleValueType.Percent,
        RuleScope scope = RuleScope.Global,
        Guid? scopeId = null, int priority = 0, bool enabled = true,
        DateOnly? from = null, DateOnly? to = null, string? name = null) => new()
    {
        Name = name ?? $"{scope} {kind} {value}",
        Scope = scope,
        ScopeId = scopeId,
        Kind = kind,
        ValueType = type,
        Value = value,
        Priority = priority,
        Enabled = enabled,
        ValidFrom = from,
        ValidTo = to,
    };

    private static PriceBreakdown Calc(params PricingRule[] rules) =>
        PricingEngine.Calculate(Money.Eur(1000m), null, Context(), rules);

    [Fact]
    public void No_rules_sell_equals_net()
    {
        var result = Calc();
        Assert.Equal(Money.Eur(1000m), result.SellPrice);
        Assert.Equal(Money.Zero("EUR"), result.AgencyCommission);
        Assert.Empty(result.Steps);
    }

    [Fact]
    public void Global_percent_markup_then_fixed_fee()
    {
        var result = Calc(
            Rule(RuleKind.Markup, 15m),
            Rule(RuleKind.Fee, 25m, RuleValueType.FixedAmount));

        // 1000 * 1.15 = 1150; + 25 fee = 1175
        Assert.Equal(Money.Eur(1175m), result.SellPrice);
        Assert.Equal(2, result.Steps.Count);
        Assert.Equal(Money.Eur(1000m), result.NetCost);
    }

    [Fact]
    public void Markups_apply_in_priority_order()
    {
        var result = Calc(
            Rule(RuleKind.Markup, 50m, RuleValueType.FixedAmount, priority: 1, name: "second"),
            Rule(RuleKind.Markup, 10m, priority: 0, name: "first"));

        // (1000 * 1.10) + 50 = 1150 — NOT (1000 + 50) * 1.10 = 1155
        Assert.Equal(Money.Eur(1150m), result.SellPrice);
        Assert.Equal("first", result.Steps[0].Description);
    }

    [Fact]
    public void Program_scope_beats_global_for_the_same_kind()
    {
        var result = Calc(
            Rule(RuleKind.Markup, 15m),
            Rule(RuleKind.Markup, 20m, scope: RuleScope.Program, scopeId: ProgramId));

        Assert.Equal(Money.Eur(1200m), result.SellPrice);
        Assert.Single(result.Steps);
    }

    [Fact]
    public void Agency_scope_beats_program_and_global()
    {
        var result = Calc(
            Rule(RuleKind.Markup, 15m),
            Rule(RuleKind.Markup, 20m, scope: RuleScope.Program, scopeId: ProgramId),
            Rule(RuleKind.Markup, 8m, scope: RuleScope.Agency, scopeId: AgencyId));

        Assert.Equal(Money.Eur(1080m), result.SellPrice);
    }

    [Fact]
    public void Scope_win_is_per_kind_not_global()
    {
        var result = Calc(
            Rule(RuleKind.Markup, 10m, scope: RuleScope.Agency, scopeId: AgencyId),
            Rule(RuleKind.Fee, 30m, RuleValueType.FixedAmount)); // global fee still applies

        Assert.Equal(Money.Eur(1130m), result.SellPrice);
    }

    [Fact]
    public void Disabled_and_out_of_validity_rules_are_ignored()
    {
        var result = Calc(
            Rule(RuleKind.Markup, 99m, enabled: false),
            Rule(RuleKind.Markup, 99m, to: Travel.AddDays(-1)),
            Rule(RuleKind.Markup, 99m, from: Travel.AddDays(1)),
            Rule(RuleKind.Markup, 10m, from: Travel, to: Travel));

        Assert.Equal(Money.Eur(1100m), result.SellPrice);
        Assert.Single(result.Steps);
    }

    [Fact]
    public void Commission_is_computed_on_sell_price_without_changing_it()
    {
        var result = Calc(
            Rule(RuleKind.Markup, 20m),
            Rule(RuleKind.Commission, 10m));

        Assert.Equal(Money.Eur(1200m), result.SellPrice);
        Assert.Equal(Money.Eur(120m), result.AgencyCommission);
    }

    [Fact]
    public void Rounding_happens_per_step_away_from_zero()
    {
        var result = PricingEngine.Calculate(
            Money.Eur(333.33m), null, Context(333.33m),
            [Rule(RuleKind.Markup, 12.5m)]);

        // 333.33 * 1.125 = 374.99625 → 375.00
        Assert.Equal(Money.Eur(375.00m), result.SellPrice);
    }

    [Fact]
    public void Conversion_step_is_preserved_first_in_the_breakdown()
    {
        var fx = new AppliedStep(Guid.NewGuid(), "FX USD->EUR @ 0.9", "Conversion",
            new Money(1111.11m, "USD"), Money.Eur(1000m));

        var result = PricingEngine.Calculate(Money.Eur(1000m), fx, Context(), [Rule(RuleKind.Markup, 10m)]);

        Assert.Equal("Conversion", result.Steps[0].Kind);
        Assert.Equal(Money.Eur(1100m), result.SellPrice);
    }
}
