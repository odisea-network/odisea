# Odisea

**B2B travel platform** — a tour operator builds dynamic packaged offers (accommodation, transport, transfer, insurance, extras) and distributes them to partner travel agencies, which search live-priced packages and book them.

> v2 reboot — fresh start. The previous white-label widget platform is preserved on the [`archive/v1`](../../tree/archive/v1) branch (tag `v1.0-final`).

## Architecture (planned)

- **Backend** — .NET 10 modular monolith (`backend/`): Agencies, Catalog, Pricing, Booking, Integrations modules over a single PostgreSQL database (schema per module).
- **Integrations** — provider-agnostic reservation contract (`IReservationProvider`): Mock adapter first; external systems (TourVisio, Sejour, …) plug in behind the same interface.
- **Frontend** — Angular 21 B2B portal (`frontend/portal`): agency area (search, booking wizard, bookings) and admin area (agencies, programs, pricing rules).

## Status

Phase 0 — repository reset. Scaffolding lands in Phase 1 (walking skeleton).
