using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Pricing.Domain;
using Odisea.Modules.Pricing.Features.Calculator;
using Odisea.Modules.Pricing.Features.Rules;
using Odisea.Modules.Pricing.Infrastructure;

namespace Odisea.Modules.Pricing.Controllers;

[ApiController]
[Route("api/v1/exchange-rates")]
[Authorize(Policy = AuthPolicies.Operator)]
public class ExchangeRatesController(PricingDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ExchangeRateDto>>> List(CancellationToken ct) =>
        Ok(await db.ExchangeRates
            .OrderBy(r => r.FromCurrency).ThenByDescending(r => r.AsOf)
            .Select(r => r.ToDto())
            .ToListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<ExchangeRateDto>> Create(SaveExchangeRateRequest request, CancellationToken ct)
    {
        var rate = new ExchangeRate
        {
            FromCurrency = request.FromCurrency.ToUpperInvariant(),
            ToCurrency = PriceCalculator.BaseCurrency,
            Rate = request.Rate,
            AsOf = request.AsOf,
        };
        db.ExchangeRates.Add(rate);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), null, rate.ToDto());
    }
}
