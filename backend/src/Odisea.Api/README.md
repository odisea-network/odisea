# Odisea.Api

The **single host** of the modular monolith. It owns cross-cutting HTTP concerns and wires the five modules together; it contains **no business logic**.

## Responsibilities

- `Program.cs` — composition root: Serilog, ProblemDetails, controller discovery from module assemblies (application parts), the five `Add<Name>Module(config)` calls, Swagger (with Bearer auth button), migrate-on-startup + seeding
- `CurrentUser` — implements `ICurrentUser` from JWT claims for anything that needs the caller's identity
- `Controllers/HealthController` — `/health` (anonymous by design)
- `Dockerfile` — multi-stage image used by `compose.yaml` (build context = repo root)
- Configuration: `appsettings.json` (dev connection string, JWT issuer/audience), `appsettings.Development.json` (dev operator seed). **Secrets never live here** — `Jwt:Secret` comes from user-secrets (local) or environment (compose/prod)

## Startup order

migrate each module's DbContext → seed (operator admin from `Seed:Operator:*`) → serve. The app **refuses to start** if `Jwt:Secret` is missing or shorter than 256 bits.

## Relations

References all five modules and SharedKernel. Modules never reference the host.
