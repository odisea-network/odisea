# Odisea.SharedKernel

The **tiny** set of base types every module builds on. Deliberately minimal: no EF Core, no HTTP, no DI registrations, no business logic. If a type is useful to exactly one module, it belongs in that module — not here.

## Contents

| Type | Role |
|---|---|
| `Entity` | Base for all persisted entities: version-7 `Guid` id (time-ordered → append-only Postgres inserts) + `CreatedAt` |
| `Money` | Amount + ISO currency; refuses cross-currency arithmetic (`CurrencyMismatchException`) |
| `DateRange` | Validated from/to with `Nights`, `Contains`, `Overlaps` — travel stays, seasons, rule validity |
| `Pax` | Adults + children with invariants (≥1 adult) |
| `IClock` / `SystemClock` | Injectable time — lockouts, token expiry and pricing validity are all testable with a fake clock |
| `ICurrentUser` | The authenticated caller (id, agency, role) — implemented by the host from JWT claims |
| `DomainException` | Base for all typed domain exceptions; controllers map them to RFC 7807 `Problem(...)` |

## Relations

Referenced by **every** module and the host. References **nothing** except the base framework. Module-to-module contracts do NOT live here — they live in each owning module's `PublicApi` namespace.
