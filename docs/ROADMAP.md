# Odisea v2 — Implementation Roadmap

> The committed version of the reboot plan (2026-10-05). The [project board](https://github.com/orgs/odisea-network/projects/1) is the source of truth for individual tasks; this file tracks the phase-level picture. Update the status column as phases complete.

## Phases

| Phase | Deliverable | Status |
|---|---|---|
| **0 — Reset** | v1 archived (`archive/v1`, `v1.0-final`), fresh `main`, thesis backed up, tooling retooled, board rebuilt | ✅ done (2026-10-05) |
| **1 — Walking skeleton** | Solution + modules (#95 ✅) · Agencies/auth (#96 ✅) · compose + CI + CodeQL + Dependabot (#97 ✅) · Angular portal shell: login, guards, role shells (#98 ✅) | ✅ done (2026-10-22) |
| **2 — Catalog + mock search** | Catalog CRUD (#108 ✅) · provider contract + Mock adapter + call logging (#107 ✅) · live-priced `POST /api/v1/search` + protected offer tokens (#110 ✅) · agency search UI (#132 ✅) | ✅ done (2026-10-23) |
| **3 — Pricing engine** | Rules + rates CRUD, pure engine, breakdown, sell prices in search (#109 ✅) | ✅ done (2026-10-09) |
| **4 — Booking flow vs mock** | Full lifecycle, state machine, history, jsonb breakdown, sequence refs, operator oversight (#111 ✅) · booking wizard + bookings UI (#132 ✅) | ✅ done (2026-10-23) |
| **5 — Real provider adapter** | TourVisio/Sejour adapter behind the same contract · content sync → mapping screens · resilience. **Gated on: intermediary decision + sandbox credentials — the only blocker** | ⬜ gated |
| **6 — Operations** | Hardening (#112 ✅) · **Documents module: фактури/известия per ЗДДС with gapless numbering, margin scheme, PDFs (#119 ✅)** · refresh-token rotation (#135 ✅) · DataProtection keys in the DB (#140 ✅) · **reservation lifecycle: full passenger data with encrypted documents (#142 ✅), submit → provider hold → payments → admin confirmation, operator oversight (#144 ✅)** · API keys for external agencies (#147 ✅) · operator branding endpoint for the white-label portal (#157 ✅) · **portal on the reservation contract with runtime branding (#145 ✅)** · admin UI: bookings oversight (#160 ✅) · **portal design system, themed sidebar shell and re-skinned screens (#176 ✅)** · **contract documents: Word templates filled from booking/agency data, agency self-service (#166 ✅)** · **agency workspace tier: e-mail outbox (#168 ✅), plans & entitlements (#169 ✅), external reservations + public tracking (#170 ✅), inquiry forms (#171 ✅)** · **agency portal phase 2: search cards and compare, 4-step wizard, booking detail, phone layouts (#178 ✅)** · **admin UI: agencies/users/API keys/branding (#161 ✅), catalog (#162 ✅), pricing (#163 ✅), documents and company profile (#164 ✅)** · **operator Today dashboard with server-side figures (#184 ✅)** · remaining: pricing simulator endpoint, workspace screens (external reservations, inquiry forms, e-mail outbox), audit log, auto-expiry job, vouchers, credit-limit enforcement, deployment | 🔄 in progress |

## Cross-cutting: security baseline — issue #100

Done: PBKDF2 hashing, lockout, anti-enumeration login, JWT validation, deny-by-default authorization, CodeQL + Dependabot, auth rate limiting (10/min/IP), security headers, strict CORS allowlist, HSTS outside dev, PII-free provider call logs, DataProtection-wrapped offer tokens (net cost never leaves the backend) with the key ring persisted in `agencies.data_protection_keys`, Cache-Control no-store on /api, dev-gated Swagger, CI-enforced net-leak guard over agency-facing DTOs.
Done also: refresh-token rotation with reuse detection (#135 — 15-min access tokens, HttpOnly SameSite=Strict cookie, family revocation on replay) · DataProtection key ring persisted in the database (#140) · passenger document numbers encrypted at rest and never logged (#142) · `OperatorAdmin` policy for money decisions, actor on every status change, optimistic concurrency on bookings (#144) · per-agency API keys for external systems with hashed storage, revocation and per-key rate limiting (#147).
Open: audit log for sensitive operations, encrypt the persisted DataProtection key ring at rest (#153), pre-prod pen-test gate.

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
