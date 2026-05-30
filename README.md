# Odisea Network

B2B travel offers platform for the Bulgarian market. Travel **agencies** and tour
**operators** post travel packages ("offers") and define reusable, filtered
**Collections** of those offers. Agencies embed lightweight **web components** into
their own websites; those components render Collections to travelers.

> Status: **Increment 1 of 4, Scaffold.** One thin vertical slice runs end to end.
> Filter engine, Collection builder UI, package posting form, parameter binding,
> auth, and theming all land in later increments.

## Architecture (Increment 1)

```
                 ┌────────────────────────────────────┐
                 │   Agency website (someone else's)  │
                 │   <script src="…/loader.js">       │
                 │   <tp_offer_list collection="…"/>  │
                 └─────────────┬──────────────────────┘
                               │ fetch /api/v1/collections/{slug}/offers
                               ▼
┌────────────┐   /api    ┌───────────────────────────────────────────────┐   EF   ┌────────────┐
│  Angular   │──────────►│  ASP.NET Core 10 Web API (modular monolith)   │───────►│ Postgres17 │
│  portal    │           │  • /health, /swagger, /api/v1/*               │        │ snake_case │
│  (nginx)   │           │  • serves /demo + /components (static)        │        └────────────┘
└────────────┘           │  • Catalog module: Offer, Collection, Filter  │
                         │  • Tenancy module: Agency, Operator (seed)    │
                         └───────────────────────────────────────────────┘
```

* **Clean Architecture** layout: `Odisea.Domain` (entities, value objects, enums) →
  `Odisea.Application` (DTOs, business logic, `IAppDbContext` abstraction) →
  `Odisea.Infrastructure` (EF Core, `AppDbContext`, migrations, seeding) →
  `Odisea.WebAPI` (Program.cs, minimal API endpoints). Dependencies always
  flow inward; `Odisea.Domain` references nothing.
* One shared `AppDbContext` in Infrastructure implements `IAppDbContext` (defined in
  Application). Endpoints depend on the interface, not on EF Core directly.
* EF Core 10 with `EFCore.NamingConventions` → all tables and columns are
  `snake_case`. Migrations are applied automatically on startup **in Development
  only**, then idempotent seed data is inserted on a clean DB.
* The Lit web components package is **not** its own service. It is built once
  (Node 22 inside the API's Dockerfile, Stage A) and copied into
  `wwwroot/components/`, which the API serves as static files.
* Demo "agency websites" (`agency_blue.html`, `agency_green.html`) are
  handwritten static pages committed under `wwwroot/demo/`; each loads the
  component via `<script src="/components/loader.js">` and themes it through
  CSS custom properties.

## Repository layout

```
odisea/
  compose.yaml
  backend/
    Odisea.sln
    global.json                       # pins SDK 10.0.300
    src/
      Odisea.Domain/                  # entities, value objects, enums (no dependencies)
        Common/Entity.cs              # base class with Id, CreatedAt, UpdatedAt
        Entities/                     # Offer, Collection, Agency, Operator
        ValueObjects/FilterSpec.cs    # FilterSpec, FilterCondition, SortSpec, ParameterDef
        Enums/                        # OwnerType, Visibility, BoardBasis, Transport, *Status
      Odisea.Application/             # depends on Domain
        Common/Interfaces/IAppDbContext.cs
        Catalog/Dtos/Dtos.cs          # OfferDto, CollectionDto, CreateCollectionRequest, Mappings
        Catalog/Filtering/FilterResolver.cs
        Catalog/Collections/CollectionResolver.cs
        DependencyInjection.cs        # AddApplicationServices()
      Odisea.Infrastructure/          # depends on Application + Domain
        Data/AppDbContext.cs          # implements IAppDbContext
        Data/Seeder.cs                # idempotent dev seed
        Data/Configurations/          # IEntityTypeConfiguration<T> per entity
        Data/Migrations/              # EF Core migrations
        DependencyInjection.cs        # AddInfrastructureServices(IConfiguration)
      Odisea.WebAPI/                  # depends on Infrastructure + Application
        Program.cs                    # composition root
        Endpoints/                    # HealthEndpoints, OffersEndpoints, CollectionsEndpoints
        wwwroot/demo/                 # static demo agency pages (committed)
        wwwroot/components/           # loader.js committed; bundles built in Docker (gitignored)
    tests/
      Odisea.UnitTests/               # xUnit tests
  frontend/
    components/             # Lit 3 + TS + Vite library package
    portal/                 # Angular 21 management portal (CSR, no SSR)
  docs/
```

## Tech stack

* **.NET 10** (SDK 10.0.300), ASP.NET Core minimal APIs, Serilog console sink,
  Swashbuckle (Swagger).
* **EF Core 10** with Npgsql + EFCore.NamingConventions → snake_case.
* **PostgreSQL 17** (alpine image).
* **Lit 3** + TypeScript, bundled with Vite in library mode (ESM + UMD).
* **Angular 21** (CSR only, no SSR).
* **Docker** plus a root `compose.yaml`.

## Running the full stack

Build and start everything via Docker Compose: `docker compose up build`
(invoke the build flag using the long form of the compose flag your shell
prefers). Then:

| URL                                                | What you should see                                      |
|                                                    |                                                          |
| http://localhost:8080/health                       | `{"status":"healthy"}`                                   |
| http://localhost:8080/swagger                      | Swagger UI listing every endpoint                        |
| http://localhost:8080/api/v1/offers                | JSON array of ~14 seeded offers                          |
| http://localhost:8080/api/v1/collections/summer_greece/offers | Greece only subset                            |
| http://localhost:8080/demo/agency_blue.html        | Blue themed embed of `summer_greece`                     |
| http://localhost:8080/demo/agency_green.html       | Green themed embed of `last_minute_egypt`                |
| http://localhost:4200                              | Angular portal: Offers + Collections + Builder (stub)    |

The `portal` container reverse proxies `/api/*` and `/health` to `api:8080`, so
the browser sees a single origin on port 4200.

## Running pieces locally for development

The full stack is the easy path; these are for tighter feedback loops.

**Backend** (needs .NET 10 SDK plus a running Postgres on localhost:5432):

```
cd backend
dotnet run project src/Odisea.WebAPI
```

**Components** (live rebuild of the Lit bundle):

```
cd frontend/components
npm install
npm run build         # writes to dist/, copy to ../../backend/src/Odisea.WebAPI/wwwroot/components/
```

**Portal** (Angular dev server with `/api` proxied):

```
cd frontend/portal
npm install
npx ng serve          # http://localhost:4200
```

## API surface (Increment 1)

| Method | Path                                          | Notes                                             |
|        |                                               |                                                   |
| GET    | `/health`                                     | liveness probe                                    |
| GET    | `/api/v1/offers?country=&city=&maxPrice=`     | published offers, light query params              |
| GET    | `/api/v1/offers/{id}`                         |                                                   |
| GET    | `/api/v1/collections`                         |                                                   |
| GET    | `/api/v1/collections/{idOrSlug}`              |                                                   |
| GET    | `/api/v1/collections/{idOrSlug}/offers`       | resolve filter → exclude → sort → prepend pinned  |
| POST   | `/api/v1/collections`                         | create a Collection (no auth in Increment 1)      |

Errors return RFC 7807 `ProblemDetails`. Unknown filter `field` or `op` → `400`.

## FilterSpec (Increment 1)

`FilterSpec` is **flat**: a list of `FilterCondition` records, all ANDed
together.

```jsonc
{
  "all": [
    { "field": "country", "op": "eq",       "value": "GR" },
    { "field": "maxPrice","op": "lte",      "value": 700 },
    { "field": "tag",     "op": "contains", "value": "luxury" }
  ]
}
```

Whitelisted fields: `country`, `city`, `maxPrice`, `board`, `transport`, `tag`.
Whitelisted ops: `eq`, `in`, `lte`, `contains`.

Each `(field, op)` pair is translated to typed EF Core LINQ in
`Filtering/FilterResolver.cs`; we **never** build raw SQL from input. Unknown
field or op → `400 ProblemDetails`.

Nested `any` / `all` groups and parameter substitution land in Increment 2.

## Seed data

`Seeder.SeedAsync` runs on startup in Development if the DB is empty:

* 2 agencies: `blue_horizon`, `green_path`.
* 1 operator: `sun_operators`.
* ~14 published offers across `BG`, `GR`, `TR`, `EG` with varied price,
  board, transport, and tags. A mix of `PlatformShared` operator offers and
  `AgencyPrivate` agency owned offers.
* 3 collections with stable slugs:
  1. `summer_greece` (`country = GR`)
  2. `last_minute_egypt` (`country = EG`, `maxPrice <= 700`)
  3. `blue_horizon_vip` (`tag contains "luxury"`)

## Assumptions made during Increment 1

* `.NET 10 SDK 10.0.300` is the target; the Docker image is `mcr.microsoft.com/dotnet/sdk:10.0`.
* EF and Npgsql versions are pinned to `10.0.0` and `EFCore.NamingConventions`
  to `10.0.0`.
* Solution file is the classic `.sln` format (the .NET 10 default `.slnx`
  was opted out of, matching the spec).
* Enums are persisted as strings (not ints) for readability.
* JSON value comparers for `jsonb` columns use a cheap
  serialize and compare strategy; fine for the increment, replace with
  proper structural comparers in Increment 2 if it ever shows up in profiling.
* Two `RouteHandlerAnalyzer` warnings appear at build time because the
  endpoint registration is generic on `TDb`; a known analyzer NRE in the
  current ASP.NET tooling, benign.
* The portal's `/builder` route is a stub component pointing at Increment 2.
* No auth, no API key enforcement; CORS is wide open in Development.

## Out of scope for Increment 1

Authentication and API keys, the Collection builder UI, the package posting form,
parameter substitution and binding, nested filter groups, payments, bookings,
AI, microservices, message queues, marketing site, multi DbContext separation.

## What is ready for Increment 2

* Filter engine extension surface (`FilterResolver.Apply`); add an `any`/`all`
  branch and a parameter substitution pass before whitelist checking.
* Collection persistence is already wired for `Parameters` (`List<ParameterDef>`).
* Stable Collection slugs the builder UI can target (`summer_greece`,
  `last_minute_egypt`, `blue_horizon_vip`).
* A second Lit component slot in `frontend/components/src/` for theming aware
  variants.
* An empty `/builder` route in the Angular portal as the entry point for the
  Collection builder UI.
