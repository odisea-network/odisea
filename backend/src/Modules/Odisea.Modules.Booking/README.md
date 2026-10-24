# Odisea.Modules.Booking

**The money moment.** Owns the reservation lifecycle from draft to confirmed (or rejected, expired, cancelled), the passengers, the payments received, and who did what along the way. *(Implemented against the mock provider.)*

## Owns

- **Domain:** `Booking` (human ref `ODI-2027-000123`, agency/agent/program/departure/hotel Guids, occupancy from the offer envelope, provider refs, net + sell price, breakdown jsonb, payment deadline), `BookingPassenger`, `BookingPayment`, `BookingStatusHistory` (with actor)
- **Passengers:** title, gender, Latin names as in the travel document, birth date, nationality (ISO-2), travel document (type, number, issuing country, expiry), lead flag, lead contact (email, phone). Everything beyond the names may arrive later via `PUT /api/v1/bookings/{id}/passengers` (replace-all, while Draft/PriceConfirmed). Passenger type is derived at check-in: under 2 → Infant, under 12 → Child, else Adult (infants count as children until `Pax` learns about them). **Document numbers are encrypted at rest** (DataProtection converter, purpose `Odisea.Booking.PassengerDocument`, key ring persisted by Agencies) and never appear in logs, notes or error messages — validation speaks of "Passenger 2", never of names or numbers.
- **Offer envelope:** the protected token a client holds binds the raw provider token to the agency, hotel and occupancy it was issued for; `CreateDraft` rejects tokens issued to another agency or hotel and takes adults/children from the envelope, never from the request.
- **Scoping:** agency users see ONLY their agency's bookings — filtered by the `agency_id` JWT claim, never by a client-supplied id. Not-found and not-yours are the same 404.
- **Persistence:** `BookingDbContext` → schema `booking`. Optimistic concurrency on `bookings` rides on Postgres `xmin` (`uint Version`, no column of its own); a stale write becomes a 409.

## The lifecycle

```
Draft ─re-price─▶ PriceConfirmed ─submit─▶ PendingConfirmation ─provider OK─▶ AwaitingPayment ─admin confirm (fully paid)─▶ Confirmed
  │ cancel (agency)  │ cancel (agency)  │ provider KO → Failed            │ reject (operator)                      │ cancel (admin)
  ▼                  ▼                  │ cancel (admin rescue)           │ expire (operator, past PaymentDueAt)   ▼
Cancelled          Cancelled            ▼                                 │ cancel (agency or admin)             Cancelled
                                      Cancelled                          ▼
                                                                  Rejected / Expired / Cancelled
```

Enforced in one place: `Booking.TransitionTo(next, actor, note, now)` throws `InvalidBookingTransitionException` and appends a history row carrying the actor (`Agency`, `ApiClient`, `Operator`, `System`) and the user id. `Rejected`, `Expired`, `Cancelled`, `Failed` are terminal.

**Submit** (`POST /api/v1/bookings/{id}/submit`) runs `Booking.EnsureReadyForSubmit()` — party matches the priced occupancy (count and adult/child mix at check-in), every passenger has gender, birth date, nationality and a travel document valid through check-out, exactly one lead with email and phone, price confirmed — then persists `PendingConfirmation` **before** calling the provider and persists the outcome after. The two saves are deliberate: a crash mid provider call leaves a visible row operators can reconcile instead of a silently lost reservation. On success the provider holds the reservation and `PaymentDueAt` is set from `Booking` options (`PaymentOptionDays`, `PaymentDueDaysBeforeCheckIn`, `MinimumPaymentWindowHours`; see `PaymentDeadline`).

**Payments** are records, not a number: `BookingPayment` (amount, currency, method, reference, received date, who recorded it). A mistaken entry is voided with a reason, never deleted, and voided payments do not count. `PaidAmount` and `BalanceDue` are computed. A payment cannot exceed the balance due or change currency.

**Confirmation** is the operator's commercial decision, available only once the reservation is fully paid (`PaymentIncompleteException` → 409 with the balance). Documents reads this moment (`IBookingLookup.ConfirmedAt`) as the tax event for the invoice.

**Agency cancellation** is allowed while Draft, PriceConfirmed or AwaitingPayment with no payment recorded yet (the provider hold is released). Once money has been received, once Confirmed, or while the provider call is in flight, cancelling means refunds, credit notes and possibly provider penalties, so it becomes an operator-admin action. Payment writes also mark the booking row modified so the `xmin` check covers them: two operators recording payments at the same time cannot overpay.

## Operator oversight (`/api/v1/admin/bookings`)

| Endpoint | Policy | Notes |
|---|---|---|
| `GET ?status&agencyId&from&to&page&pageSize` | Operator | paged `OperatorBookingSummaryDto`; `from`/`to` filter check-in |
| `GET {id}` | Operator | `OperatorBookingDto`: net cost, breakdown, payments, history with actors |
| `POST {id}/confirm` | **OperatorAdmin** | 409 unless AwaitingPayment and fully paid |
| `POST {id}/reject` `{note}` | Operator | releases the provider hold → Rejected |
| `POST {id}/expire` | Operator | only once `PaymentDueAt` has passed → Expired |
| `POST {id}/cancel` `{note}` | **OperatorAdmin** | from PendingConfirmation / AwaitingPayment / Confirmed |
| `POST {id}/payments` | Operator | 201; AwaitingPayment only |
| `POST {id}/payments/{paymentId}/void` `{reason}` | **OperatorAdmin** | soft void |

`Operator*` DTOs are the only shapes that carry the net cost; `NetCostLeakGuardTests` scans every other DTO in this namespace and fails the build on a leak.

## Relations

The one module that consumes others (PublicApi only): **Agencies** (agency active check, `OperatorAdmin` policy; credit-limit enforcement is still open), **Catalog** (program/hotel validation), **Pricing** (re-price breakdown), **Integrations** (offer envelope, re-price/book/cancel against the provider; Booking maps its own passenger enums onto the provider contract so the persisted domain never couples to it). **Documents** consumes `IBookingLookup` (status, sell amount, confirmation and cancellation moments).

Open: automatic expiry job (today `expire` is an operator action), vouchers, credit-limit enforcement.
