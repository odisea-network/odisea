# Odisea Portal

The **B2B web app** — one Angular 21 application with two role-based areas over the same API: the agency portal (search, book, documents) and the operator back-office. Standalone components, signals, zoneless, Vitest.

## Layout

```
src/app/
  core/        auth.service (access token in memory, HttpOnly refresh cookie), auth.interceptor
               (Bearer header, single-flight refresh on 401), guards (agencyGuard, adminGuard),
               branding.service (operator name, colours, logo from GET /api/v1/branding at startup),
               api.types (DTO mirrors), format (display-only money/date helpers), problem (ProblemDetails)
  layout/      shared Shell (branded header, nav per role, legal footer) + AgencyShell / AdminShell
  features/
    auth/      login page, branded; redirects by user_type claim
    agency/    search (program/departure/pax -> live-priced offers), book (wizard: occupancy-sized
               passenger form with travel documents and one lead -> draft -> re-price with drift
               warning -> submit -> awaiting payment), bookings (list with paid/due and payment
               deadline, history with actors, cancel while unpaid), documents (фактури + PDF download)
    admin/     bookings oversight over /api/v1/admin/bookings: filtered, paged list across agencies;
               detail with net cost, price breakdown, passengers, payments and the actor trail;
               record payment, confirm, reject, expire, cancel, void payment (booking-actions.ts
               mirrors the API's state + role rules so a button only shows when the call can succeed)
    placeholder.ts for the remaining admin routes whose API already exists but UI doesn't yet
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
npm test                  # vitest (auth, branding, guards, passenger validation, wizard rules, operator actions, app)
npx ng build              # production build, budgets enforced
```

## White-label branding

One build serves every operator deployment. At startup `BrandingService` loads `GET /api/v1/branding` (anonymous, so the login page is branded too), writes the primary colour into `--accent` and the accent colour into `--accent-2`, sets the document title and favicon, and exposes the name, logo and support contact to the shell and login page. Operators change all of it through the API; nothing brand-specific is compiled in. If the API is unreachable the portal falls back to neutral defaults.

## Operator back-office

`/admin/bookings` is the first real admin screen. The list is served filtered and paged by the API (status, agency, check-in range). The detail page shows what agencies never see — net cost, the stored pricing breakdown, agency commission — plus payments and every status change with its actor. Actions follow the backend's rules exactly: any operator records payments, rejects, or expires a reservation past its deadline; confirming (only once the balance due is zero), cancelling and voiding a payment appear only for users with role `Admin`, and the API enforces the same with the `OperatorAdmin` policy, so hiding a button is a courtesy, not the control.

## Security notes

- **Money is never computed client-side** — every amount (sell price, commission, totals) arrives calculated from the API; `formatMoney` only formats, and a CI guard test keeps net-cost fields out of agency-facing DTOs.
- The access token (15 min) lives **in memory only** — nothing script-readable is persisted. The session rides an HttpOnly SameSite=Strict refresh cookie scoped to /api/v1/auth; reloads restore via silent refresh, 401s retry once after a single-flight refresh, and replaying a used refresh token revokes the whole token family server-side.
- The API's CORS allowlist covers `http://localhost:4200` in Development; in compose, nginx same-origin-proxies `/api` so CORS never applies.

## Relations

Talks ONLY to the backend API (`/api/v1/...`). The v1 Lit widget library is gone — this is a portal, not an embeddable.
