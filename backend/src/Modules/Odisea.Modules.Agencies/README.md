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
