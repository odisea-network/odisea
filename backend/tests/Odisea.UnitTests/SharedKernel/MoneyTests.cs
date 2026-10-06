using Odisea.SharedKernel;
using Xunit;

namespace Odisea.UnitTests.SharedKernel;

public class MoneyTests
{
    [Fact]
    public void Normalizes_currency_to_uppercase()
    {
        var money = new Money(10m, "eur");
        Assert.Equal("EUR", money.Currency);
    }

    [Theory]
    [InlineData("")]
    [InlineData("EURO")]
    [InlineData("E")]
    public void Rejects_invalid_currency_codes(string currency)
    {
        Assert.Throws<ArgumentException>(() => new Money(10m, currency));
    }

    [Fact]
    public void Adds_amounts_in_same_currency()
    {
        var total = Money.Eur(850m) + Money.Eur(150m);
        Assert.Equal(Money.Eur(1000m), total);
    }

    [Fact]
    public void Refuses_to_combine_different_currencies()
    {
        Assert.Throws<CurrencyMismatchException>(() => Money.Eur(10m) + new Money(10m, "USD"));
    }

    [Fact]
    public void Multiplies_and_rounds_away_from_zero()
    {
        var price = (Money.Eur(100m) * 1.185m).Round();
        Assert.Equal(Money.Eur(118.50m), price);
    }
}
