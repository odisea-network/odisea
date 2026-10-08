# Odisea.Modules.Agencies

**Who may use the platform, and as what.** Identity, authentication, and the agency ledger-side master data: agencies, their users, credit limits, commission levels.

## Owns

- **Domain:** `Agency` (status, country, credit limit EUR, commission level), `AgencyUser` / `OperatorUser` over abstract `UserAccount` — which owns the lockout state machine (5 failures → 15 min)
- **Auth:** `POST /api/v1/auth/login` → JWT (HS256). `TokenService` issues claims (`sub`, `email`, `name`, `role`, `user_type`, `agency_id`); `AuthService` implements hardened login: PBKDF2 verify with rehash, anti-enumeration (identical error + dummy hash for unknown/inactive accounts), lockout enforcement
- **CRUD:** `/api/v1/agencies` and `/api/v1/agencies/{id}/users` — operator-only
- **Persistence:** `AgenciesDbContext` → schema `agencies`
- **Authorization for everyone:** the deny-by-default fallback policy and the `Operator`/`Agency` policies are registered here

## PublicApi (what other modules may use)

- `AuthPolicies` — policy names for `[Authorize(Policy = ...)]`
- `AuthClaims` — claim type names (e.g. Booking reads `agency_id` to scope data)

Future (when Booking needs it): credit-limit check contract.

## Relations

Depends only on SharedKernel. Booking will depend on this module's PublicApi for credit checks; every module uses its policies. Security rationale and remaining hardening: issue #100.
