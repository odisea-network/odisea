# Odisea.Modules.Integrations

**The gateway to the outside world.** Every external reservation system (TourVisio, Sejour, bedbanks, …) is one adapter behind one provider-neutral contract. The rest of the platform does not know or care which provider answered. *(Skeleton — contract + Mock adapter land in Phase 2, the first real adapter in Phase 5.)*

## Will own

- **The contract (`PublicApi`):** `IReservationProvider` — search, re-price, book, cancel, status; `IProviderContentSource` — hotel/location catalog sync; `IProviderRegistry` — resolve adapter by provider code
- **The opaque token rule:** `ProviderOfferToken` carries provider-specific state through search → re-price → book. Only the issuing adapter can parse it. Provider internals never leak past this module.
- **Mock adapter** (first-class citizen): deterministic seeded inventory, configurable latency, N% availability failures, M% re-price drift — so booking-flow edge cases are genuinely exercised long before real credentials exist
- **Adapter hygiene:** one `HttpClient` per adapter via `IHttpClientFactory` + resilience (retry/timeout); every call logged to `integrations.provider_call_logs` (duration, status, **redacted** payloads — no PII, no credentials); provider credentials in config/user-secrets, never plaintext in the DB
- **Persistence:** `IntegrationsDbContext` → schema `integrations` (call logs only; adapters are stateless)

## Relations

Depends only on SharedKernel. Catalog translates internal ↔ external codes around calls into this module; Booking drives the reservation operations. Which real system we integrate first ("системата посредник") is an open business decision — the contract is deliberately indifferent.
