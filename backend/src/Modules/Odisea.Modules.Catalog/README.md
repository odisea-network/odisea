# Odisea.Modules.Catalog

**What we sell.** The operator's product master data: where we go, which hotels we work with, and which packaged programs agencies can book. *(Skeleton — content lands in Phase 2.)*

## Will own

- **Domain:** `Destination`, `Hotel` (internal master records), `Program` (season, destination, provider, Draft→Published→Archived), `ProgramDeparture` (date range, transport), `ProgramHotel` (the allowed-hotels list per program)
- **Mappings:** `HotelMapping` / `LocationMapping` — the translation table between OUR ids and each external provider's codes. Catalog owns this because hotels are our master data; Integrations stays provider-generic. Unmapped provider results are dropped + logged, never shown half-mapped.
- **CRUD:** operator-only program/hotel/destination management; agency-visible published-program search support
- **Persistence:** `CatalogDbContext` → schema `catalog`

## PublicApi (planned)

Program/hotel lookups for Booking (existence, allowed-hotel validation) and mapping translation for the search flow.

## Relations

Depends only on SharedKernel. Pricing scopes rules by `ProgramId` (plain Guid); Booking references programs/departures/hotels by Guid; the search feature translates internal ↔ external codes before/after calling Integrations.
