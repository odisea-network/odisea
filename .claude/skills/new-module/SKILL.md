---
name: new-module
description: Scaffold or flesh out an Odisea backend module following the house conventions (PublicApi boundary, per-module DbContext/schema, feature folders, module DI extension). Use when creating a new module or adding the first real content to a skeleton module.
---

# Build an Odisea module the house way

Reference implementation: `Odisea.Modules.Agencies`. Architecture rules: `docs/ARCHITECTURE.md` + CLAUDE.local.md §3.

## Layout (folders, not sub-projects)

```
Odisea.Modules.<Name>/
  PublicApi/          interfaces + record DTOs other modules may use — THE ONLY importable namespace
  Domain/             entities (inherit SharedKernel.Entity), enums, typed DomainExceptions
  Features/<Feature>/ feature services + *Dtos.cs with ToDto() extension methods
  Infrastructure/     <Name>DbContext (+ Configurations/, Data/Migrations/, Data/ seeders)
  Controllers/        MVC controllers, [Route("api/v1/...")], primary-constructor DI
  <Name>Module.cs     services.Add<Name>Module(config)
  README.md           purpose + relations — update it in the same PR
```

## Checklist

1. csproj: `FrameworkReference Microsoft.AspNetCore.App` + SharedKernel reference + (if persisting) Npgsql/NamingConventions packages (versions from Directory.Packages.props).
2. DbContext: `HasDefaultSchema("<name>")`, `ApplyConfigurationsFromAssembly`; registered in the module extension with `MigrationsHistoryTable("__ef_migrations_history", "<schema>")` + `UseSnakeCaseNamingConvention()`.
3. Conventions: enums `HasConversion<string>()`, string max lengths, DTOs are records, no AutoMapper, no repository pattern — services/controllers take the module DbContext directly.
4. Controllers: `[Authorize(Policy = AuthPolicies.Operator|Agency)]` explicitly (the fallback policy only guarantees *authenticated*, not *authorized*). Agency-scoped queries filter by the `agency_id` claim via `ICurrentUser` — never a client-supplied id.
5. Host wiring (`Program.cs`): `AddApplicationPart(typeof(<Name>Module).Assembly)` + `Add<Name>Module(configuration)` + the DbContext in the migrate-on-startup block.
6. Tests: behavior tests (InMemory, FakeClock) for domain/service logic. The module-boundary architecture test picks the module up automatically via `ModuleNames` — add the name there if it's brand new.
7. First migration via the `add-migration` skill; verify live via the `run-stack` skill.
8. Update `docs/ARCHITECTURE.md` module table + the module README.
