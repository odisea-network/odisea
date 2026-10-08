# Odisea Backend

.NET 10 **modular monolith**. One deployable host, five modules with enforced boundaries, one PostgreSQL database with a schema per module. See [docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md) for the full picture.

## Projects

| Project | Purpose |
|---|---|
| [src/Odisea.Api](src/Odisea.Api/README.md) | The single host: composition root, middleware, Swagger, health |
| [src/Odisea.SharedKernel](src/Odisea.SharedKernel/README.md) | Base types shared by all modules (no EF, no HTTP) |
| [src/Modules/Odisea.Modules.Agencies](src/Modules/Odisea.Modules.Agencies/README.md) | Identity, JWT auth, agencies, users, credit limits |
| [src/Modules/Odisea.Modules.Catalog](src/Modules/Odisea.Modules.Catalog/README.md) | Destinations, hotels, programs, provider code mappings |
| [src/Modules/Odisea.Modules.Pricing](src/Modules/Odisea.Modules.Pricing/README.md) | Pricing rules, exchange rates, the pricing engine |
| [src/Modules/Odisea.Modules.Booking](src/Modules/Odisea.Modules.Booking/README.md) | Bookings, passengers, status state machine, documents |
| [src/Modules/Odisea.Modules.Integrations](src/Modules/Odisea.Modules.Integrations/README.md) | Provider contract + adapters for external reservation systems |
| [tests/Odisea.UnitTests](tests/Odisea.UnitTests/README.md) | Unit + architecture tests |

## Run it

```bash
docker compose up -d db                        # from the repo root; Postgres 17 on host port 5433
dotnet run --project src/Odisea.Api            # migrates, seeds, listens on http://localhost:8080
```

- **Swagger:** http://localhost:8080/swagger — click **Authorize**, paste the token from `POST /api/v1/auth/login`
- **Dev login** (Development seeding only): `admin@odisea.local` / `DevOnly-Odisea-Admin-2026!`
- **JWT secret:** `dotnet user-secrets set "Jwt:Secret" "<random ≥32 bytes>"` in `src/Odisea.Api` (the app refuses to start without a strong secret; compose sets a dev-only value for the containerized API)

## Verify

```bash
dotnet build Odisea.slnx     # warnings are errors
dotnet test Odisea.slnx      # includes module-boundary architecture tests
```

## Add a migration (per module)

```bash
dotnet ef migrations add <Name> \
  --project src/Modules/Odisea.Modules.<Module> \
  --startup-project src/Odisea.Api \
  --context <Module>DbContext \
  --output-dir Infrastructure/Data/Migrations
```
