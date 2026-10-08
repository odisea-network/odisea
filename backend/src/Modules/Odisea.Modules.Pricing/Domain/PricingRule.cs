using Odisea.SharedKernel;

namespace Odisea.Modules.Pricing.Domain;

public class PricingRule : Entity
{
    public required string Name { get; set; }
    public RuleScope Scope { get; set; } = RuleScope.Global;

    /// ProgramId or AgencyId depending on Scope; null for Global.
    public Guid? ScopeId { get; set; }

    public RuleKind Kind { get; set; }
    public RuleValueType ValueType { get; set; }

    /// Percent (e.g. 15 = +15%) or a fixed EUR amount, depending on ValueType.
    public decimal Value { get; set; }

    /// Lower runs first within a kind.
    public int Priority { get; set; }

    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public bool Enabled { get; set; } = true;

    public bool AppliesOn(DateOnly travelDate) =>
        Enabled
        && (ValidFrom is null || travelDate >= ValidFrom)
        && (ValidTo is null || travelDate <= ValidTo);
}

public enum RuleScope
{
    Global,
    Program,
    Agency,
}

public enum RuleKind
{
    Markup,
    Fee,
    Commission,
}

public enum RuleValueType
{
    Percent,
    FixedAmount,
}
