---
name: run-stack
description: Boot the Odisea dev stack (Postgres via compose + API) and smoke-test it, ending with a ready-to-use Swagger URL. Use when the user says "run the app", "start the stack", "boot the api", "open swagger", or wants to manually test endpoints.
---

# Run the Odisea dev stack

## Steps

1. **Database** (from the repo root):
   ```powershell
   docker compose up -d db
   docker compose ps   # wait for "healthy"
   ```
   If Docker Desktop isn't running, start it first (`Start-Process "C:\Program Files\Docker\Docker\Docker Desktop.exe"`) and wait ~30 s for `docker info` to answer.

2. **API** — two options:
   - Host-side (fast iteration): `dotnet run --project backend/src/Odisea.Api` — uses `localhost:5433` and the user-secrets `Jwt:Secret`.
   - Containerized (prod-like): `docker compose up -d --build api` — uses the compose dev JWT secret.

   Startup migrates every module DbContext and seeds the dev operator (Development only).

3. **Smoke** (all must pass before claiming "it runs"):
   ```bash
   curl -s http://localhost:8080/health                       # -> healthy
   curl -s -o /dev/null -w "%{http_code}" http://localhost:8080/api/v1/agencies   # -> 401 (deny-by-default)
   # login -> token -> authorized call -> 200
   ```

4. **Hand the user Swagger:** http://localhost:8080/swagger — click **Authorize**, paste the `accessToken` from `POST /api/v1/auth/login`. Dev login: `admin@odisea.local` / `DevOnly-Odisea-Admin-2026!`.

## Gotchas

- Port 5433 (not 5432) on the host — a native Windows Postgres may hold 5432.
- `Jwt:Secret` missing/short → the app refuses to start by design. Set it: `dotnet user-secrets set "Jwt:Secret" "<random ≥32 bytes>"` in `backend/src/Odisea.Api`.
- Fresh empty DB after `docker compose down -v` is fine — migrate-on-startup rebuilds it; never comment that block out.
- Stop a host-side API on Windows with `Get-Process -Name Odisea.Api | Stop-Process -Force`.
