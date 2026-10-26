# Odisea Portal

The **B2B web app** — one Angular 21 application with two role-based areas over the same API: the agency portal (search, book, documents) and the operator back-office. Standalone components, signals, zoneless, Vitest.

## Layout

```
src/
  styles.scss        global entry: tokens, reset, utilities and the design-system primitives
  styles/            _tokens.scss (brand + derived inks, neutrals, semantic colours, radii, shadows),
                     _mixins.scss (glass, inset, focus-ring, breakpoints), _reset.scss, _utilities.scss,
                     primitives/ (_button, _input, _surfaces, _table - skins for native elements)
  app/
    core/        auth.service (access token in memory, HttpOnly refresh cookie), auth.interceptor
                 (Bearer header, single-flight refresh on 401), guards (agencyGuard, adminGuard),
                 branding.service (name, colours, logo from GET /api/v1/branding at startup),
                 theme (contrast maths that derives readable inks from the two brand colours),
                 api.types (DTO mirrors), format (display-only money/date/deadline helpers),
                 booking-history (status history -> timeline), problem (ProblemDetails)
    shared/ui/   the component kit, one folder per component with its own .scss (see below)
    shared/pipes/ money, odDate, odDateTime - thin wrappers over core/format
    layout/      Shell (sidebar on wide screens, top bar + drawer below 900px, bottom tab bar on phones
                 for the agency area, legal footer),
                 SideNav (brand block, one primary action, grouped links, signed-in user),
                 AgencyShell / AdminShell supply the navigation groups
    features/
      auth/      login page, branded; error and lockout states; redirects by user_type claim
      agency/    search (program/departure/pax -> live-priced offers as cards or a table, sort,
                 compare up to three side by side), book (4-step wizard: travellers -> live price
                 with drift acceptance -> submit -> result; resumes a saved draft via ?bookingId),
                 booking-detail (/agency/bookings/:id: next step, travellers with masked documents,
                 package, documents, payment, history, cancel while unpaid), bookings (saved views,
                 card list on phones), documents (invoices + PDF download)
      admin/     bookings oversight over /api/v1/admin/bookings: filtered, paged list across agencies;
                 detail with a "next step" card, net cost, price breakdown, payments (inline record
                 form), travellers with masked document numbers, decisions and the actor timeline
                 (booking-actions.ts mirrors the API's state + role rules so a button only shows
                 when the call can succeed; disabled actions say why)
      placeholder.ts for the remaining admin routes whose API already exists but UI doesn't yet
```

Routing: `/login`, `/agency/**` (agencyGuard), `/admin/**` (adminGuard), lazy `loadComponent` everywhere, route `data.title` bound via component input binding.

## Design system

The look comes from the approved design canvas (glass surfaces on a neutral stage, Inter, tabular
figures, one primary action per view, status never carried by colour alone). Rules of the road:

- **SCSS only, one stylesheet per component** (`styleUrl: './x.scss'`), kept under the 4 kB
  `anyComponentStyle` budget. Shared values are CSS custom properties from `styles/_tokens.scss`;
  shared recipes are mixins from `styles/_mixins.scss` (`@use 'mixins' as *;` resolves through
  `stylePreprocessorOptions.includePaths`).
- **Native elements, skinned by directives:** `<button uiButton="primary|secondary|ghost|danger|danger-quiet" size="sm|lg" block iconOnly>`,
  `<input uiInput>` / `<select uiInput>` / `<textarea uiInput>`. Their styles are global primitives
  because the elements live in feature templates.
- **Components for everything with structure:** `ui-page-header` (crumbs, title, `[status]` and
  `[actions]` slots), `ui-card` (`title`, `[cardActions]`, `flush` for tables), `ui-field`
  (label + hint/error, `for` the control id), `ui-status-chip` (icon + label per booking status),
  `ui-pill`, `ui-banner`, `ui-deadline` (absolute + relative, urgency by words), `ui-timeline`,
  `ui-progress`, `ui-masked` (reveal on click), `ui-segmented`, `ui-pager`, `ui-skeleton`,
  `ui-empty-state`, `ui-icon` (inline stroke icons from `icon/icons.ts`).
- **Feedback:** `ToastService.show()` for confirmations after an action; `DialogService.confirm()`
  (native `<dialog>`, optional required reason and restated facts) instead of `window.confirm`.
  Both hosts are mounted once in the app root.
- Inter is self-hosted (`@fontsource-variable/inter`, latin + cyrillic subsets) so an operator
  deployment never calls a third party from the login page.

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
npm test                  # vitest (auth, branding + theme derivation, guards, deadlines, passenger validation, saved views, history timeline, operator actions, app)
npx ng build              # production build, budgets enforced
```

## White-label branding

One build serves every operator deployment. At startup `BrandingService` loads `GET /api/v1/branding` (anonymous, so the login page is branded too), sets the document title and favicon, and exposes the name, logo and support contact to the shell and login page. The two brand colours become five CSS tokens on `:root`: `--primary` and `--accent` as given, plus `--primary-ink` / `--accent-ink` (the colour darkened in 10 % steps until it reaches 4.8:1 on white, used for links, active navigation and text) and `--on-primary` (white, or near-black when the primary is too light). `core/theme.ts` holds the contrast maths with tests, so any operator palette stays readable. Operators change all of it through the API; nothing brand-specific is compiled in. If the API is unreachable the portal falls back to the design defaults.

## Operator back-office

`/admin/bookings` is the first real admin screen. The list is served filtered and paged by the API (status, agency, check-in range). The detail page opens with the next step (collect the balance, confirm, or nothing to do) and shows what agencies never see — net cost, the stored pricing breakdown, agency commission — plus payments and every status change with its actor. Actions follow the backend's rules exactly: any operator records payments, rejects, or expires a reservation past its deadline; confirming (only once the balance due is zero), cancelling and voiding a payment are offered only to users with role `Admin`, and the API enforces the same with the `OperatorAdmin` policy, so a disabled button (always with its reason) is a courtesy, not the control. Reject, cancel and void collect a reason in the confirmation dialog.

## Security notes

- **Money is never computed client-side** — every amount (sell price, commission, totals) arrives calculated from the API; `formatMoney` only formats, and a CI guard test keeps net-cost fields out of agency-facing DTOs.
- The access token (15 min) lives **in memory only** — nothing script-readable is persisted. The session rides an HttpOnly SameSite=Strict refresh cookie scoped to /api/v1/auth; reloads restore via silent refresh, 401s retry once after a single-flight refresh, and replaying a used refresh token revokes the whole token family server-side.
- The API's CORS allowlist covers `http://localhost:4200` in Development; in compose, nginx same-origin-proxies `/api` so CORS never applies.

## Relations

Talks ONLY to the backend API (`/api/v1/...`). The v1 Lit widget library is gone — this is a portal, not an embeddable.
