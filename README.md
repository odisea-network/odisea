# Odisea

**B2B travel platform** — a tour operator builds dynamic packaged offers (accommodation, transport, transfer, insurance, extras) and distributes them to partner travel agencies, which search live-priced packages and book them.

> v2 reboot — fresh start. The previous white-label widget platform is preserved on the [`archive/v1`](../../tree/archive/v1) branch (tag `v1.0-final`).

## Architecture (planned)

- **Backend** — .NET 10 modular monolith (`backend/`): Agencies, Catalog, Pricing, Booking, Integrations modules over a single PostgreSQL database (schema per module).
- **Integrations** — provider-agnostic reservation contract (`IReservationProvider`): Mock adapter first; external systems (TourVisio, Sejour, …) plug in behind the same interface.
- **Frontend** — Angular 21 B2B portal (`frontend/portal`): agency area (search, booking wizard, bookings) and admin area (agencies, programs, pricing rules).

## Layout

```
backend/
  Odisea.slnx                      .NET 10 solution (slnx format, VS 17.14+)
  Directory.Build.props            shared build settings (nullable, warnings as errors)
  Directory.Packages.props         central package versions
  src/
    Odisea.Api/                    host: Serilog, Swagger, ProblemDetails, /health
    Odisea.SharedKernel/           Entity, Money, DateRange, Pax, IClock, ICurrentUser
    Modules/
      Odisea.Modules.Agencies/     identity, agencies, users, credit limits, commissions
      Odisea.Modules.Catalog/      destinations, hotels, programs, departures, mappings
      Odisea.Modules.Pricing/      pricing rules, exchange rates, pricing engine
      Odisea.Modules.Booking/      bookings, passengers, status history, documents
      Odisea.Modules.Integrations/ provider contract + adapters (Mock, TourVisio, …)
  tests/
    Odisea.UnitTests/              value-object tests + module-boundary architecture test
```

Each module keeps a `PublicApi/` namespace — the only surface other modules may reference (enforced by an architecture test). One Postgres database, one schema per module.

## Status

Phase 1 — walking skeleton in progress. Backend solution scaffold is up (`dotnet build` / `dotnet test` in `backend/`); Agencies module, dev stack, and portal shell are next (#96–#98).
