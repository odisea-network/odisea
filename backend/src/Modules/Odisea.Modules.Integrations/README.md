# Odisea.Modules.Integrations

**The gateway to the outside world.** Every external reservation system (TourVisio, Sejour, bedbanks, …) is one adapter behind one provider-neutral contract. The rest of the platform does not know or care which provider answered. *(Contract + Mock adapter + call logging: implemented. First real adapter: Phase 5, gated on the provider decision.)*

## Owns

- **The contract (`PublicApi`):** `IReservationProvider` — search, re-price, book, cancel, status; `IProviderRegistry` — resolve adapter by provider code (case-insensitive), always wrapped in the call-logging decorator. (`IProviderContentSource` for catalog sync arrives with the first real adapter — not speculated earlier.)
- **The opaque token rule:** `ProviderOfferToken` carries provider-specific state through search → re-price → book. Only the issuing adapter can parse it. Provider internals never leak past this module.
- **Mock adapter** (first-class citizen): deterministic seeded inventory, configurable latency, N% availability failures, M% re-price drift — so booking-flow edge cases are genuinely exercised long before real credentials exist
- **Adapter hygiene:** one `HttpClient` per adapter via `IHttpClientFactory` + resilience (retry/timeout); every call logged to `integrations.provider_call_logs` (duration, status, **redacted** payloads — no PII, no credentials); provider credentials in config/user-secrets, never plaintext in the DB
- **Persistence:** `IntegrationsDbContext` → schema `integrations` (call logs only; adapters are stateless)

## Relations

Depends only on SharedKernel. Catalog translates internal ↔ external codes around calls into this module; Booking drives the reservation operations. Which real system we integrate first ("системата посредник") is an open business decision — the contract is deliberately indifferent.
