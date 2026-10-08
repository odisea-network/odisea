using System.ComponentModel.DataAnnotations;
using Odisea.Modules.Pricing.Domain;

namespace Odisea.Modules.Pricing.Features.Rules;

public record PricingRuleDto(
    Guid Id, string Name, string Scope, Guid? ScopeId, string Kind, string ValueType,
    decimal Value, int Priority, DateOnly? ValidFrom, DateOnly? ValidTo, bool Enabled);

public record SavePricingRuleRequest(
    [Required, MaxLength(200)] string Name,
    [Required] RuleScope Scope,
    Guid? ScopeId,
    [Required] RuleKind Kind,
    [Required] RuleValueType ValueType,
    [Range(-1000, 1000)] decimal Value,
    [Range(0, 1000)] int Priority,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    bool Enabled = true);

public record ExchangeRateDto(Guid Id, string FromCurrency, string ToCurrency, decimal Rate, DateOnly AsOf);

public record SaveExchangeRateRequest(
    [Required, RegularExpression("^[A-Za-z]{3}$")] string FromCurrency,
    [Range(0.000001, 1_000_000)] decimal Rate,
    [Required] DateOnly AsOf);

public static class RuleDtoMapping
{
    public static PricingRuleDto ToDto(this PricingRule r) => new(
        r.Id, r.Name, r.Scope.ToString(), r.ScopeId, r.Kind.ToString(), r.ValueType.ToString(),
        r.Value, r.Priority, r.ValidFrom, r.ValidTo, r.Enabled);

    public static ExchangeRateDto ToDto(this ExchangeRate r) =>
        new(r.Id, r.FromCurrency, r.ToCurrency, r.Rate, r.AsOf);
}
