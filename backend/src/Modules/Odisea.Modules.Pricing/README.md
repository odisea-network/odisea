# Odisea.Modules.Pricing

**What it costs.** Turns a provider's net cost into the final B2B sell price: our markup + fees, the agency's commission, currency conversion. *(Implemented: rules + rates CRUD, pure engine, IPriceCalculator.)*

## Owns

- **Domain:** `PricingRule` (scope Global|Program|Agency; kind Markup|Fee|Commission; percent or fixed; priority; validity window), `ExchangeRate` (manual entry first; EUR is the base currency)
- **The engine:** a **pure, deterministic, zero-I/O function** — `Calculate(PricingContext, rules) → PriceBreakdown`. Fixed order: currency conversion → markups (by priority) → fees → commission. Most-specific scope wins (Agency > Program > Global). Pure = exhaustively unit-testable.
- **Auditability:** the full `PriceBreakdown` (every applied rule and intermediate value) is handed to Booking and persisted with the booking — the price can be explained forever, even after rules change
- **CRUD:** operator-only rule management
- **Persistence:** `PricingDbContext` → schema `pricing`

## PublicApi

`IPriceCalculator` for the search flow and for Booking's re-price step.

## Relations

Depends only on SharedKernel (`Money`, `Pax`, `DateRange` do the heavy lifting). Consumes nothing from other modules — rules reference programs/agencies by plain Guid.
