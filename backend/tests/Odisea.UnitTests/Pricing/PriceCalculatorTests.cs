using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Pricing.Domain;
using Odisea.Modules.Pricing.Features.Calculator;
using Odisea.Modules.Pricing.Infrastructure;
using Odisea.Modules.Pricing.PublicApi;
using Odisea.SharedKernel;
using Xunit;

namespace Odisea.UnitTests.Pricing;

public class PriceCalculatorTests
{
    private readonly PricingDbContext _db = new(
        new DbContextOptionsBuilder<PricingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private PriceCalculator Calculator => new(_db);

    private static PricingContext UsdContext => new(
        new Money(1000m, "USD"), Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2027, 3, 30));

    [Fact]
    public async Task Foreign_net_cost_is_converted_with_the_latest_rate_before_travel()
    {
        _db.ExchangeRates.AddRange(
            new ExchangeRate { FromCurrency = "USD", ToCurrency = "EUR", Rate = 0.95m, AsOf = new DateOnly(2027, 1, 1) },
            new ExchangeRate { FromCurrency = "USD", ToCurrency = "EUR", Rate = 0.90m, AsOf = new DateOnly(2027, 3, 1) },
            new ExchangeRate { FromCurrency = "USD", ToCurrency = "EUR", Rate = 0.80m, AsOf = new DateOnly(2027, 5, 1) });
        _db.SaveChanges();

        var result = await Calculator.CalculateSellPriceAsync(UsdContext, CancellationToken.None);

        Assert.Equal(Money.Eur(900m), result.SellPrice); // 0.90 wins: latest AsOf <= travel date
        Assert.Equal("Conversion", result.Steps[0].Kind);
        Assert.Equal(new Money(1000m, "USD"), result.Steps[0].Before);
    }

    [Fact]
    public async Task Missing_rate_throws_typed_exception()
    {
        await Assert.ThrowsAsync<MissingExchangeRateException>(
            () => Calculator.CalculateSellPriceAsync(UsdContext, CancellationToken.None));
    }

    [Fact]
    public async Task Rules_outside_the_context_scope_are_not_applied()
    {
        _db.PricingRules.AddRange(
            new PricingRule { Name = "other program", Scope = RuleScope.Program, ScopeId = Guid.NewGuid(), Kind = RuleKind.Markup, ValueType = RuleValueType.Percent, Value = 99m },
            new PricingRule { Name = "other agency", Scope = RuleScope.Agency, ScopeId = Guid.NewGuid(), Kind = RuleKind.Markup, ValueType = RuleValueType.Percent, Value = 99m },
            new PricingRule { Name = "global", Scope = RuleScope.Global, Kind = RuleKind.Markup, ValueType = RuleValueType.Percent, Value = 10m });
        _db.SaveChanges();

        var context = new PricingContext(Money.Eur(100m), Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2027, 3, 30));
        var result = await Calculator.CalculateSellPriceAsync(context, CancellationToken.None);

        Assert.Equal(Money.Eur(110m), result.SellPrice);
    }
}
