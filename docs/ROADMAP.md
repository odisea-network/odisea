# Odisea v2 — Implementation Roadmap

> The committed version of the reboot plan (2026-10-05). The [project board](https://github.com/orgs/odisea-network/projects/1) is the source of truth for individual tasks; this file tracks the phase-level picture. Update the status column as phases complete.

## Phases

| Phase | Deliverable | Status |
|---|---|---|
| **0 — Reset** | v1 archived (`archive/v1`, `v1.0-final`), fresh `main`, thesis backed up, tooling retooled, board rebuilt | ✅ done (2026-10-05) |
| **1 — Walking skeleton** | Solution + 5 modules + SharedKernel (#95 ✅) · Agencies module with hardened JWT auth (#96 ✅) · compose + CI + CodeQL + Dependabot (#97 ✅) · Angular portal shell (#98) | 🔄 in progress |
| **2 — Catalog + mock search** | Catalog CRUD (destinations, hotels, programs, departures, mappings) · `IReservationProvider` contract + Mock adapter + registry · `POST /api/v1/search` · agency search UI | ⬜ |
| **3 — Pricing engine** | PricingRule CRUD · pure engine (currency → markups → fees → commission, most-specific scope wins) · breakdown persisted · sell prices in search | ⬜ |
| **4 — Booking flow vs mock** | Draft → re-price (drift!) → confirm → booked · cancel · status state machine · bookings UI | ⬜ |
| **5 — Real provider adapter** | TourVisio/Sejour adapter behind the same contract · content sync → mapping screens · call logging + resilience. **Gated on: intermediary decision + sandbox credentials** | ⬜ |
| **6 — Operations** | Vouchers/PDFs · credit-limit enforcement · payments tracking · security hardening completion (#100) · deployment | ⬜ |

## Cross-cutting: security baseline — issue #100

Done: PBKDF2 hashing, lockout, anti-enumeration login, JWT validation, deny-by-default authorization, CodeQL + Dependabot.
Open: rate limiting, refresh-token rotation, security headers, HSTS, strict CORS, audit log, call-log redaction, pre-prod pen-test gate.

## Standing decisions

- .NET 10 **modular monolith** · Angular 21 single app · PostgreSQL (schema per module) · EUR pricing baseline
- Mock provider first; the real intermediary system (TourVisio vs Sejour, both SAN TSG) is undecided and only blocks Phase 5
- Every phase ends runnable; every slice passes build (warnings=errors) + tests + live Swagger smoke before merge

## Definition of done per slice

1. `dotnet build` — 0 warnings, 0 errors
2. `dotnet test` — green (new behavior covered)
3. Live verification against real Postgres (Swagger flow for API slices)
4. Docs updated when structure changed ([ARCHITECTURE.md](ARCHITECTURE.md), per-project READMEs)
5. PR references its issue; board item moved

## Origins

Business vision: the founder's Excalidraw whiteboard (4-tier architecture + "Antalya Easter" example flow). Original session plan file: `~/.claude/plans/ok-so-you-will-melodic-hinton.md`. v1 (master's thesis, white-label widget platform): branch `archive/v1`.
