# Odisea.Modules.Agencies

**Who may use the platform, and as what.** Identity, authentication, and the agency ledger-side master data: agencies, their users, credit limits, commission levels.

## Owns

- **Domain:** `Agency` (status, country, credit limit EUR, commission level), `AgencyUser` / `OperatorUser` over abstract `UserAccount` — which owns the lockout state machine (5 failures → 15 min)
- **Auth:** `POST /api/v1/auth/login` → JWT (HS256). `TokenService` issues claims (`sub`, `email`, `name`, `role`, `user_type`, `agency_id`); `AuthService` implements hardened login: PBKDF2 verify with rehash, anti-enumeration (identical error + dummy hash for unknown/inactive accounts), lockout enforcement
- **CRUD:** `/api/v1/agencies` and `/api/v1/agencies/{id}/users` — operator-only
- **Persistence:** `AgenciesDbContext` → schema `agencies`, including the DataProtection key ring (`data_protection_keys`)
- **DataProtection:** registered here (`SetApplicationName("Odisea")`, `PersistKeysToDbContext<AgenciesDbContext>`) so offer tokens and encrypted passenger data survive restarts and are shared across instances. Keys are not yet encrypted at rest — `ProtectKeysWithCertificate` before production (#100). Other modules consume `IDataProtectionProvider` as a framework service.
- **Authorization for everyone:** the deny-by-default fallback policy and the `Operator`/`Agency` policies are registered here

## PublicApi (what other modules may use)

- `AuthPolicies` — policy names for `[Authorize(Policy = ...)]`
- `AuthClaims` — claim type names (e.g. Booking reads `agency_id` to scope data)

- `IAgencyLookup` / `AgencySnapshot` — read-only agency facts (active?, credit limit) consumed by Booking.

## Relations

Depends only on SharedKernel. Booking consumes `IAgencyLookup` (active check; credit-limit enforcement is still open); every module uses its policies, and the host relies on the DataProtection registration made here. Security rationale and remaining hardening: issue #100.

## API keys for external agencies

Partner agencies that integrate directly (no portal login) authenticate with a per-agency key in the `X-Api-Key` header.

- **Issuing:** operators call `POST /api/v1/agencies/{agencyId}/api-keys` (name, optional expiry). The response is the only time the secret (`odk_live_` + 43 base64url chars) is shown; the database stores a SHA-256 hash and a 16-character prefix for display. `GET` lists keys (prefix, created, expires, revoked, last used), `DELETE .../{keyId}` revokes (idempotent).
- **Authenticating:** the default authentication scheme is a selector — a request with `X-Api-Key` goes to the `ApiKey` handler, anything else to JWT bearer (header wins). A valid, unrevoked, unexpired key of an active agency yields the same identity shape as an agency login (`user_type=agency`, `agency_id`, `sub` = key id, `role=ApiClient`, `auth_method=api_key`), so every agency-scoped endpoint and policy works unchanged, operator endpoints stay unreachable, and the auth endpoints issue no session. Rejections are a uniform 401 with `WWW-Authenticate: ApiKey`.
- **Trail:** `ICurrentUser.IsApiClient` lets Booking record the `ApiClient` actor; `LastUsedAt` is written at most every five minutes.
- **Rate limit:** `RateLimiting:ApiKeyPerMinute` (default 60) per presented key, partitioned by a hash of the header because the limiter runs before authentication — unknown keys are throttled too.

## Operator branding (white-label portal)

Each tour operator runs its own Odisea deployment and dresses the shared portal build through the API, so a brand change is never a release.

- **One row** (`agencies.operator_branding`, fixed id): display name, primary and accent colours (`#rrggbb`), support email/phone, legal footer, and a raster logo (PNG/JPEG/WebP, max 512 KB, type detected from the bytes — SVG is excluded because it can carry scripts) stored with its SHA-256 hash.
- **Reads are anonymous and cacheable:** `GET /api/v1/branding` (`Cache-Control: public, max-age=300`; defaults until branded) and `GET /api/v1/branding/logo` (ETag, 304 on `If-None-Match`, a day of caching; the DTO's `logoVersion` is the cache-busting key). The login page needs both before any token exists, so nothing confidential belongs in them.
- **Writes are OperatorAdmin:** `PUT /api/v1/branding`, `PUT /api/v1/branding/logo` (multipart field `file`), `DELETE /api/v1/branding/logo`.
