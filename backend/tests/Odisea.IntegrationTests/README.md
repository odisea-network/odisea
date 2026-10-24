# Odisea.IntegrationTests

HTTP-level tests that boot the real host (`WebApplicationFactory<Program>`) against a real Postgres database. They cover what the InMemory unit tests cannot: authentication and authorization over HTTP, the Postgres booking-ref sequence, migrations, and multi-module flows end to end.

## Running

```bash
docker compose up -d db          # from the repo root; Postgres 17 on host port 5433
dotnet test Odisea.slnx          # from backend/; runs unit + integration tests
```

- Connection string: `ODISEA_TEST_CONNECTION` env var, else `Host=localhost;Port=5433;Database=odisea_test;Username=odisea;Password=odisea`. The compose `odisea` role is the container superuser, so the test database can be dropped and recreated.
- The database is **dropped and recreated once per test run** (`OdiseaApiFactory.InitializeAsync`). Never point it at a database you care about.
- CI provides a `postgres:17-alpine` service (`.github/workflows/ci.yml`).

## How the factory works

- Environment `Testing`; configuration overridden in memory (connection string, a test JWT secret, `Seed:DemoData=true`, `Seed:Operator:*`).
- Touching `Server` runs `Program.cs`, which migrates every module context and runs `AgenciesSeeder` + `DevDemoSeeder`. The factory then inserts a second operator with role `Operator` (no endpoint creates operators) and logs in **once per identity**, caching the tokens.
- `ClientAs(TestIdentity.Admin | Operator | Agent)` returns an authenticated `HttpClient`; `AnonymousClient()` returns a bare one.
- All test classes share the fixture through `[Collection(ApiCollection.Name)]`, so they run sequentially against one database. Tests must be order-independent: create what you need, don't assume counts.
- `AgencyFlow` holds the agency happy path (catalog → search → draft) for tests that need a booking.

## Gotchas

- If Postgres is down, `Program.cs` catches the startup exception, logs `Fatal` and returns, and the factory fails with an opaque "host never built" timeout. Check the compose db first.
- The `auth` rate limiter partitions on the client IP, which is `null` under `TestServer`: every login in the run shares one 10-per-minute bucket. Reuse `ClientAs(...)` instead of logging in inside tests.
- Use `response.ShouldBeAsync(HttpStatusCode.X)` (`HttpAssert`) rather than `EnsureSuccessStatusCode()` so the ProblemDetails body appears in the failure message.
