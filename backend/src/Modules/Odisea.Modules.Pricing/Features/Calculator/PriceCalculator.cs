using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Pricing.Domain;
using Odisea.Modules.Pricing.Features.Engine;
using Odisea.Modules.Pricing.Infrastructure;
using Odisea.Modules.Pricing.PublicApi;
using Odisea.SharedKernel;

namespace Odisea.Modules.Pricing.Features.Calculator;

public class PriceCalculator(PricingDbContext db) : IPriceCalculator
{
    public const string BaseCurrency = "EUR";

    public async Task<PriceBreakdown> CalculateSellPriceAsync(PricingContext context, CancellationToken ct)
    {
        var (net, conversionStep) = await ConvertToBaseAsync(context.NetCost, context.TravelDate, ct);

        var candidates = await db.PricingRules
            .Where(r =>
                r.Scope == RuleScope.Global ||
                (r.Scope == RuleScope.Program && r.ScopeId == context.ProgramId) ||
                (r.Scope == RuleScope.Agency && r.ScopeId == context.AgencyId))
            .ToListAsync(ct);

        return PricingEngine.Calculate(net, conversionStep, context, candidates);
    }

    private async Task<(Money Net, AppliedStep? Step)> ConvertToBaseAsync(
        Money netCost, DateOnly travelDate, CancellationToken ct)
    {
        if (netCost.Currency == BaseCurrency)
            return (netCost, null);

        var rate = await db.ExchangeRates
            .Where(r => r.FromCurrency == netCost.Currency && r.ToCurrency == BaseCurrency && r.AsOf <= travelDate)
            .OrderByDescending(r => r.AsOf)
            .FirstOrDefaultAsync(ct)
            ?? throw new MissingExchangeRateException(netCost.Currency, BaseCurrency);

        var converted = new Money(netCost.Amount * rate.Rate, BaseCurrency).Round();
        var step = new AppliedStep(
            rate.Id,
            $"FX {netCost.Currency}->{BaseCurrency} @ {rate.Rate} (as of {rate.AsOf:yyyy-MM-dd})",
            "Conversion",
            netCost,
            converted);
        return (converted, step);
    }
}
