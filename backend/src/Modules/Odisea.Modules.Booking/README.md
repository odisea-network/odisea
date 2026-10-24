# Odisea.Modules.Booking

**The money moment.** Owns the reservation lifecycle from draft to confirmed (or cancelled), the passengers, and the documents. *(Implemented: full lifecycle against the mock provider.)*

## Owns

- **Domain:** `Booking` (human ref `ODI-2027-000123`, agency/agent/program/departure/hotel Guids, occupancy from the offer envelope, provider refs, net + sell price, breakdown jsonb), `BookingPassenger`, `BookingStatusHistory`
- **Passengers:** title, gender, Latin names as in the travel document, birth date, nationality (ISO-2), travel document (type, number, issuing country, expiry), lead flag, lead contact (email, phone). Everything beyond the names may arrive later via `PUT /api/v1/bookings/{id}/passengers` (replace-all, while Draft/PriceConfirmed); the provider call requires birth date, gender and nationality for everyone. Passenger type is derived at check-in: under 2 → Infant, under 12 → Child, else Adult (infants count as children until `Pax` learns about them). **Document numbers are encrypted at rest** (DataProtection converter, purpose `Odisea.Booking.PassengerDocument`, key ring persisted by Agencies) and never appear in logs, notes or error messages.
- **Offer envelope:** the protected token a client holds binds the raw provider token to the agency, hotel and occupancy it was issued for; `CreateDraft` rejects tokens issued to another agency or hotel and takes adults/children from the envelope, never from the request.
- **The state machine:** `Draft → PriceConfirmed → PendingConfirmation → Confirmed → Cancelled/Failed`, enforced in one place (`Booking.TransitionTo(...)` throws `InvalidBookingTransitionException`). Every transition is appended to status history.
- **The flow:** create draft → complete passengers (`PUT /passengers`) → **re-price** against the provider (price may drift — the agent confirms the new price) → book → store provider booking ref → voucher (Phase 6)
- **Scoping:** agency users see ONLY their agency's bookings — filtered by the `agency_id` JWT claim, never by a client-supplied id
- **Persistence:** `BookingDbContext` → schema `booking`

## Relations

The one module that consumes others (PublicApi only): **Agencies** (agency active check; credit-limit enforcement is still open), **Catalog** (program/hotel validation), **Pricing** (re-price breakdown), **Integrations** (offer envelope, re-price/book/cancel against the provider; Booking maps its own passenger enums onto the provider contract so the persisted domain never couples to it).
