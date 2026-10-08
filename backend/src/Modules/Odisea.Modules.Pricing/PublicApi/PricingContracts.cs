using Odisea.SharedKernel;

namespace Odisea.Modules.Pricing.PublicApi;

/// Computes the final B2B sell price from a provider net cost.
/// The breakdown explains every applied step and is meant to be persisted
/// with the booking so any historical price can be audited.
public interface IPriceCalculator
{
    Task<PriceBreakdown> CalculateSellPriceAsync(PricingContext context, CancellationToken ct);
}

public sealed record PricingContext(
    Money NetCost,
    Guid ProgramId,
    Guid AgencyId,
    DateOnly TravelDate);

public sealed record AppliedStep(
    Guid RuleId,
    string Description,
    string Kind,
    Money Before,
    Money After);

public sealed record PriceBreakdown(
    Money NetCost,
    IReadOnlyList<AppliedStep> Steps,
    Money SellPrice,
    Money AgencyCommission);
