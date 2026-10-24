# Odisea.Modules.Workspace

**The agency's own back-office inside Odisea** — the paid *Workspace* tier (see `Agencies` → plans). Agencies keep here the business they do outside the operator's packages: reservations they sold on their own site, inquiries from their website, the e-mails those trigger. Odisea stores and tracks; it does not interpret.

## Owns

- `ExternalReservation` — the agency's own reference (unique per agency), an unguessable `TrackingCode`, agency-defined `Status` (free text, default `New`), optional title / customer (name, e-mail, phone) / travel dates / tags, and a **free-form JSON payload** (`jsonb`, ≤ 64 KB, stored verbatim — whatever the agency's system wants to keep).
- `ExternalReservationEvent` — the timeline: created, status changes (from → to), notes, updates, archive/restore, each with the actor (`Agency` user or `ApiClient` key) and a public flag.
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
| `GET /api/v1/admin/workspace/usage?month=yyyy-MM` | Operator | reservations created per agency in the month — the billing input |
| `GET /api/v1/admin/workspace/external-reservations?agencyId` | Operator | read-only support view |

Writes require `AgencyFeature.ExternalReservations` (403 with `type: …/feature-not-in-plan` otherwise); reads and the public page keep working after a downgrade so an agency can always get its data out and its customers are not punished.

## Public tracking page

What shows: `Created` and every `StatusChanged` event (status is public by nature), plus notes marked public; notes on status changes only when public. Codes use an alphabet without 0/O/1/I so they survive a phone call; `-` and spaces in a typed code are ignored.

## Relations

Depends on `Agencies.PublicApi` (policies, `IAgencyEntitlements`, `IAgencyLookup` for the agency name on the public page). Inquiry forms (#171) will add `Notifications.PublicApi.IEmailOutbox`.

## Not included (yet)

Attachments, customer self-service beyond the status page, webhooks to the agency's system on status change, bulk import/export.
