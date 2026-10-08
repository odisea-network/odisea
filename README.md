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

## Quickstart

```bash
docker compose up -d db                                  # Postgres 17 (host port 5433)
dotnet run --project backend/src/Odisea.Api              # migrate + seed + listen on :8080
```

Open **http://localhost:8080/swagger**, call `POST /api/v1/auth/login` (dev: `admin@odisea.local` / `DevOnly-Odisea-Admin-2026!`), click **Authorize**, paste the token — every endpoint is now testable from the browser. Everything is 401 without a token by design.

## Documentation

| Doc | What it answers |
|---|---|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | The overall picture: tiers, module boundaries, data & auth model, provider abstraction |
| [docs/ROADMAP.md](docs/ROADMAP.md) | The implementation plan: phases, status, definition of done |
| [backend/README.md](backend/README.md) | How to run, test, and add migrations; project index |
| `backend/src/**/README.md` | Each project's purpose and relations |
| [Project board](https://github.com/orgs/odisea-network/projects/1) | Task-level source of truth |

Repo workflow skills for Claude Code live in [.claude/skills/](.claude/skills/) (`run-stack`, `add-migration`, `new-module`, `pick-task`, `start-feature`, `commit-changes`, `finish-feature`).

## Status

Phase 1 — walking skeleton. Done: solution scaffold (#95), Agencies module with hardened JWT auth (#96), dev stack + CI + CodeQL + Dependabot (#97). Next: Angular portal shell (#98). Security baseline: #100.
