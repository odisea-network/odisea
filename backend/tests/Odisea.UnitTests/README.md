# Odisea.UnitTests

Unit and architecture tests on EF InMemory. HTTP-level tests against real Postgres live in [Odisea.IntegrationTests](../Odisea.IntegrationTests/README.md).

## What lives here

- **`Architecture/`** — the boundary police: NetArchTest rules asserting every module references other modules **only** through `PublicApi` (every ordered pair of modules). If this fails, the monolith is becoming a ball of mud — fix the dependency, don't weaken the test.
- **`SharedKernel/`** — value-object invariants (`Money` currency safety, `DateRange` overlaps, `Pax`)
- **`Agencies/`** — the security-critical auth behavior: lockout after 5 failures, lockout expiry, counter reset, anti-enumeration (identical errors), token claims per user type

## Conventions

- xUnit; EF Core **InMemory** for service tests (the module is the test boundary — no repository mocks)
- `FakeClock : IClock` for anything time-dependent (lockouts, expiry) — never `Task.Delay` or real time
- Test names state behavior: `Fifth_failed_attempt_locks_the_account`
- New domain logic (pricing engine, booking state machine) lands **with** its tests in the same PR
