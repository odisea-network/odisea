---
name: add-migration
description: Add an EF Core migration for one Odisea module (per-module DbContext and schema). Use when entities or EF configurations changed in any module, or the user says "add a migration", "update the schema", "migrate".
---

# Add a per-module EF migration

Each module has its own DbContext, Postgres schema and migrations history table. Migrations ALWAYS target one module.

## Steps

1. Identify the module whose `Domain/` or `Infrastructure/Configurations/` changed.

2. From `backend/`:
   ```bash
   dotnet ef migrations add <PascalCaseName> \
     --project src/Modules/Odisea.Modules.<Module> \
     --startup-project src/Odisea.Api \
     --context <Module>DbContext \
     --output-dir Infrastructure/Data/Migrations
   ```
   No database needs to be running for `migrations add`.

3. **Review the generated migration** before committing — check: correct schema (the module's own), snake_case names, enum columns as text, expected indexes/FKs. An unexpected drop/alter means the model change was wrong — fix the model, `dotnet ef migrations remove`, regenerate.

4. Apply = run the API (migrate-on-startup), or `dotnet ef database update` with the same arguments.

5. `dotnet build Odisea.slnx && dotnet test Odisea.slnx` before committing. Commit message scope = the module, e.g. `feat(catalog): add program departure capacity`.

## Rules

- Never edit an already-merged migration — add a new one.
- A migration that touches another module's schema is a boundary violation — stop and rethink the model.
- New module's first migration? Ensure its `Add<Name>Module` registers the DbContext with `MigrationsHistoryTable("__ef_migrations_history", "<schema>")` and `UseSnakeCaseNamingConvention()` first.
