using Odisea.SharedKernel;

namespace Odisea.Modules.Pricing.Domain;

/// Manually maintained rate: 1 FromCurrency = Rate ToCurrency, valid from AsOf.
public class ExchangeRate : Entity
{
    public required string FromCurrency { get; set; }
    public required string ToCurrency { get; set; }
    public decimal Rate { get; set; }
    public DateOnly AsOf { get; set; }
}

public sealed class MissingExchangeRateException(string from, string to)
    : DomainException($"No exchange rate configured from {from} to {to}.");
