# Odisea.Modules.Workspace

**The agency's own back-office inside Odisea** — the paid *Workspace* tier (see `Agencies` → plans). Agencies keep here the business they do outside the operator's packages: reservations they sold on their own site, inquiries from their website, the e-mails those trigger. Odisea stores and tracks; it does not interpret.

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

## Relations

Depends on `Agencies.PublicApi` (policies, `IAgencyEntitlements`, `IAgencyLookup` for the agency name on the public page) and `Notifications.PublicApi` (`IEmailOutbox`).

## Not included (yet)

Attachments (form files are ignored), customer self-service beyond the status page, webhooks to the agency's system on status change or new inquiry, CAPTCHA (honeypot + rate limit only), per-form rate limits, bulk import/export.
