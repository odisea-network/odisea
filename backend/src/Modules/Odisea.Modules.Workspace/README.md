# Odisea.Modules.Workspace

**The agency's own back-office inside Odisea** — the paid *Ledger* and *Workspace* tiers (see `Agencies` → plans). Agencies keep here the business they do outside the operator's packages: reservations they sold on their own site, inquiries from their website, the e-mails those trigger. Odisea stores and tracks; it does not interpret.

## Owns

- `ExternalReservation` — the agency's own reference (unique per agency), an unguessable `TrackingCode`, agency-defined `Status` (free text, default `New`), optional title / customer (name, e-mail, phone) / travel dates / tags, and a **free-form JSON payload** (`jsonb`, ≤ 64 KB, stored verbatim — whatever the agency's system wants to keep).
- `ExternalReservationEvent` — the timeline: created, status changes (from → to), notes, updates, archive/restore, each with the actor (`Agency` user or `ApiClient` key) and a public flag.
- `InquiryForm` — a website contact form: public `AccessKey` (`odf_…`), optional origin allowlist, up to 5 notification addresses, optional auto-reply (subject/body with `{{field}}` placeholders), the field names that carry the customer's e-mail and name, a honeypot field name, enabled flag, submission and spam counters.
- `InquirySubmission` — one submission: the fields as JSON (`jsonb`, verbatim after limits), extracted customer name/e-mail, triage `Status` (`New` → `Read` / `Replied` / `Archived` / `Spam`), origin, user agent, a hash of the client address, who handled it and an internal note.
- Persistence: `WorkspaceDbContext` → schema `workspace`.

## Endpoints

| Endpoint | Who | Notes |
|---|---|---|
| `GET /api/v1/agency/external-reservations?status&q&travelFrom&travelTo&tag&includeArchived&page&pageSize` | Agency | paged, newest change first; `q` matches reference, title, customer name/e-mail |
| `GET …/statuses` | Agency | the statuses this agency has used (for filters) |
| `POST …` | Agency (Workspace plan) | 201; 409 on a duplicate reference; `payload` is any JSON |
| `GET …/{id}` | Agency | full record with payload and events |
| `PUT …/{id}` | Agency (Workspace plan) | replaces title, customer, dates, tags, payload; adds an `Updated` event |
| `POST …/{id}/status {status, note?, isPublic}` | Agency (Workspace plan) | |
| `POST …/{id}/notes {note, isPublic}` | Agency (Workspace plan) | |
| `POST …/{id}/archive`, `/unarchive` | Agency | archived records leave the default list and the public page |
| `GET /api/v1/public/track/{code}` | **anonymous** | the customer's status page: reference, title, status, agency name, travel dates, public timeline. No contact data, no payload, no ids. Any origin (CORS), 60 requests/min per IP, `Cache-Control: no-store` |
| `GET/POST /api/v1/agency/forms`, `GET/PUT/DELETE …/{id}`, `POST …/{id}/rotate-key` | Agency (Workspace plan for writes) | delete only while unused (409 otherwise — disable instead) |
| `POST /api/v1/public/forms/{accessKey}` or `POST /api/v1/public/forms` with an `access_key` field | **anonymous** | JSON object (fetch) or url-encoded / multipart text fields (HTML form); any origin (CORS), 60/min per IP; returns `{success, message, submissionId}`; with a `redirect` field on an HTML post it answers 302 to that URL when its origin is in the form's allowlist |
| `GET /api/v1/agency/inquiries?formId&status&q&from&to&page&pageSize`, `GET …/{id}`, `POST …/{id}/status {status, note?}` | Agency | default list hides Spam and Archived |
| `GET /api/v1/admin/workspace/usage?month=yyyy-MM` | Operator | external reservations and inquiry submissions per agency in the month — the billing input |
| `GET /api/v1/admin/workspace/external-reservations?agencyId` | Operator | read-only support view |

Writes require `AgencyFeature.ExternalReservations` (403 with `type: …/feature-not-in-plan` otherwise); reads and the public page keep working after a downgrade so an agency can always get its data out and its customers are not punished.

## Public tracking page

What shows: `Created` and every `StatusChanged` event (status is public by nature), plus notes marked public; notes on status changes only when public. Codes use an alphabet without 0/O/1/I so they survive a phone call; `-` and spaces in a typed code are ignored.

## Inquiry forms: how a submission is handled

1. Key must look valid and belong to an **enabled** form whose agency is active and on a plan with `InquiryForms` — otherwise 404, so the public learns nothing.
2. If the form has allowed origins, the request's `Origin` (or the `Referer`'s origin) must be one of them — otherwise 403.
3. Honeypot field filled → `SpamCount++`, nothing stored, no mail, **200 success** (bots learn nothing either).
4. Fields are cleaned (reserved `access_key` / `redirect` and the honeypot removed; ≤ 50 fields, keys of letters, digits, `_ - . [ ]` up to 60 characters, values ≤ 4000 chars, ≤ 32 KB in total) and stored as JSON; the customer's name and e-mail are read from the configured fields.
5. With `EmailNotifications` in the plan: one `inquiry-notification` mail per notification address (reply-to = the customer) and, if enabled and the customer gave a valid address, one `inquiry-auto-reply` with `{{field}}` placeholders filled — all through the Notifications outbox, never inline.

Embedding on a website:

```html
<form action="https://api.operator.example/api/v1/public/forms/odf_…" method="POST">
  <input type="hidden" name="redirect" value="https://www.agency.example/thanks">
  <input type="text" name="botcheck" style="display:none" tabindex="-1" autocomplete="off">
  <input name="name" required> <input name="email" type="email" required>
  <textarea name="message"></textarea>
  <button>Send</button>
</form>
```
or `fetch(submitPath, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(fields) })`.

## Reservation ledger

The first product sold to agencies on its own (plan **Ledger**; **Workspace** includes it). An agency registers the bookings it makes with *any* tour operator and sees, in one place, what it owes each of them, when, what commission it expects back and the papers that go with each booking. It is operational reporting, **not accounting**: no VAT, no postings, no exchange rates.

### Owns

- `TourOperator` — the agency's own list (name unique per agency, case-insensitive; contact e-mail / phone, notes). Archived operators stay on old reservations and are not offered for new ones.
- `LedgerReservation` — operator, the operator's reference (unique per agency + operator), holder name, booked-on and travel dates, **operator price** and currency, optional customer sale price (informational), status (`Draft` / `Confirmed` / `Completed` / `Cancelled`), notes, commission (type, rate, base, expected, received). Optimistic concurrency on Postgres `xmin`.
- `LedgerInstallment` — the schedule agreed with the operator: any number of rows (`Deposit` / `Balance` / `Other`), each an amount and a due date.
- `LedgerPayment` — money actually sent to the operator: amount, date, method, reference, an idempotency key; never deleted, only **voided** with a reason.
- `LedgerDocument` — a file kept with the reservation (operator contract, confirmation, invoice, payment proof, other), bytes in the row, SHA-256, optional link to one payment.
- `LedgerAuditEvent` — who changed which money-relevant value, when and why (`jsonb` field → from / to). Written in the same `SaveChanges` as the change. Holder names and notes are recorded only as "changed"; a document event keeps the file name, size and hash so a deleted file can still be identified.

### Financial rules

1. `paid` = payments that are not voided. `outstanding = max(0, operator price − paid)`; anything above the price is reported as `overpaid`, never rejected — the transfer happened.
2. **Payments are not tied to an instalment.** Whatever was paid covers the schedule in order, first instalment first, so one transfer can settle several instalments and a part-payment leaves exactly one instalment partly paid. Instalment status (`Unpaid` / `PartiallyPaid` / `Paid`, `isOverdue`) is computed, not stored.
3. An instalment is overdue the day **after** its due date. "Today" is the calendar date in `Workspace:Ledger:TimeZone` (default `Europe/Sofia`), one setting for the deployment.
4. The schedule does not have to add up to the price: `scheduleDifference` is returned and shown ("incomplete schedule"). No instalments at all is valid.
5. A deposit given as a percentage is a form helper; the API stores amounts.
6. A payment is always in the reservation's currency. Totals are **per currency** and never added across currencies.
7. Commission is informational and does not reduce what is owed to the operator. Percent of the operator price or of the customer sale price, or a typed amount; rounded to cents half away from zero. Status (`Expected` / `PartiallyReceived` / `Received`) follows the received amount the agency records.
8. Cancelling requires a note and changes no figure: fees and refunds differ per operator, so a cancelled reservation keeps counting in "owed" and "overdue" until someone corrects its price and schedule.
9. Drafts are left out of the dashboard and the due filters: nothing is owed on an unconfirmed booking.

### Permissions

| Action | Agent / API key | Agency admin |
|---|---|---|
| Read everything; create and edit reservations; edit the schedule while nothing is paid; record a payment; upload a document; record received commission; add or edit a tour operator | yes | yes |
| Void a payment; change the operator price or the schedule after a payment (reason required); delete a document; archive / restore a tour operator | no | yes |

An API key is never an agency admin. Writes need `AgencyFeature.ReservationLedger` (403 `…/feature-not-in-plan`); reads keep working after a downgrade. The agency always comes from the token: another agency's reservation, payment or document is a 404.

### Endpoints (policy `Agency`, base `/api/v1/agency`)

| Endpoint | Notes |
|---|---|
| `GET/POST tour-operators`, `PUT tour-operators/{id}`, `POST tour-operators/{id}/archive\|unarchive` | archive / unarchive: agency admin |
| `GET ledger/reservations?q&status&operatorId&travelFrom&travelTo&dueWithinDays&overdueOnly&sort&page&pageSize` | `q` matches reference, holder, operator name. The due filters look at instalments that are still open; both together = overdue or due within N days |
| `POST ledger/reservations`, `GET/PUT ledger/reservations/{id}` | `PUT` carries `version`; a stale one is 409 `stale_version` |
| `PUT ledger/reservations/{id}/schedule` | replaces the whole schedule |
| `POST ledger/reservations/{id}/payments`, `POST …/payments/{paymentId}/void` | same `idempotencyKey` twice records one payment |
| `POST ledger/reservations/{id}/status`, `PUT ledger/reservations/{id}/commission` | |
| `GET/POST ledger/reservations/{id}/documents`, `GET …/documents/{documentId}/file`, `DELETE …/documents/{documentId}` | multipart `file` + `type`; PDF / JPEG / PNG recognised by their bytes, 10 MB, 50 per reservation; always served as a download |
| `GET ledger/reservations/{id}/history` | audit events, newest first |
| `GET ledger/dashboard` | active reservations, overdue, due in 7 / 30 days, outstanding and commission per currency, the 20 most urgent open instalments |

Every rejection is a ProblemDetails with a stable `code` extension (`reference_taken`, `currency_mismatch`, `admin_required`, `stale_version`, `file_type_unsupported`, …); the portal maps the code to its own wording and never shows the English detail.

### Assumptions to confirm with the product owner

- Commission is settled separately (gross settlement): the agency pays the operator the full price and receives the commission later. Net settlement would change rule 7.
- Payments from the customer to the agency are not tracked; the customer sale price is a reference figure only.
- No exchange rates and no conversion; a reservation lives in one currency.
- Files live in Postgres (`bytea`) without a malware scan. Fine for the first agencies; object storage and scanning come before volume.
- One time zone per deployment decides "today".

### Not included (yet)

Reading a contract PDF with AI to pre-fill a reservation (needs a privacy review and a provider choice), due-date reminders (e-mail through the Notifications outbox), a read-only role, customer payments, refunds and cancellation fees as their own records, export.

## Relations

Depends on `Agencies.PublicApi` (policies, `IAgencyEntitlements`, `IAgencyLookup` for the agency name on the public page) and `Notifications.PublicApi` (`IEmailOutbox`).

## Not included (yet), external reservations and forms

Attachments (form files are ignored), customer self-service beyond the status page, webhooks to the agency's system on status change or new inquiry, CAPTCHA (honeypot + rate limit only), per-form rate limits, bulk import/export.
