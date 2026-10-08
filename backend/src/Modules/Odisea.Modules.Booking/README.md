# Odisea.Modules.Booking

**The money moment.** Owns the reservation lifecycle from draft to confirmed (or cancelled), the passengers, and the documents. *(Skeleton — content lands in Phase 4.)*

## Will own

- **Domain:** `Booking` (human ref `ODI-2027-000123`, agency/agent/program/departure/hotel Guids, provider refs, net + sell price, breakdown jsonb), `BookingPassenger`, `BookingStatusHistory`, `BookingDocument`
- **The state machine:** `Draft → PriceConfirmed → PendingConfirmation → Confirmed → Cancelled/Failed`, enforced in one place (`Booking.TransitionTo(...)` throws `InvalidBookingTransitionException`). Every transition is appended to status history.
- **The flow:** create draft → **re-price** against the provider (price may drift — the agent confirms the new price) → book → store provider booking ref → voucher (Phase 6)
- **Scoping:** agency users see ONLY their agency's bookings — filtered by the `agency_id` JWT claim, never by a client-supplied id
- **Persistence:** `BookingDbContext` → schema `booking`

## Relations

The one module that consumes others (PublicApi only): **Agencies** (credit limit checks), **Catalog** (program/hotel validation), **Pricing** (re-price breakdown), **Integrations** (search/re-price/book/cancel against the provider).
