# Odisea Portal

The **B2B web app** — one Angular 21 application with two role-based areas over the same API: the agency portal (search, book, documents) and the operator back-office. Standalone components, signals, zoneless, Vitest.

## Layout

```
src/app/
  core/        auth.service (signals + sessionStorage session), auth.interceptor
               (Bearer header, 401 → login), guards (agencyGuard, adminGuard)
  layout/      shared Shell + AgencyShell / AdminShell (nav per role)
  features/
    auth/      login page (redirects by user_type claim)
    agency/    search (program/departure/pax -> live-priced offers), book (wizard:
               passengers -> draft -> re-price with drift warning -> confirm),
               bookings (list, history, cancel), documents (фактури + PDF download)
    placeholder.ts for admin routes whose API already exists but UI doesn't yet
```

Routing: `/login`, `/agency/**` (agencyGuard), `/admin/**` (adminGuard), lazy `loadComponent` everywhere, route `data.title` bound via component input binding.

## Run it

```bash
# against a locally running API (http://localhost:8080):
npm start                 # ng serve on :4200, /api proxied via proxy.conf.json
# or the full stack:
docker compose up -d --build   # from the repo root → portal on :4200 behind nginx
```

Dev login: `agent@odisea.local` / `DevOnly-Odisea-Agent-2026!` (agency) · `admin@odisea.local` / `DevOnly-Odisea-Admin-2026!` (operator).

## Verify

```bash
npm test                  # vitest (auth service, guards, app)
npx ng build              # production build, budgets enforced
```

## Security notes

- **Money is never computed client-side** — every amount (sell price, commission, totals) arrives calculated from the API; `formatMoney` only formats, and a CI guard test keeps net-cost fields out of agency-facing DTOs.
- The JWT lives in **sessionStorage** (dies with the tab) and is attached only by the interceptor. Refresh-token rotation is tracked in #100 and will shorten access-token life.
- The API's CORS allowlist covers `http://localhost:4200` in Development; in compose, nginx same-origin-proxies `/api` so CORS never applies.

## Relations

Talks ONLY to the backend API (`/api/v1/...`). The v1 Lit widget library is gone — this is a portal, not an embeddable.
