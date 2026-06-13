# Odisea Webhooks

Outbound webhooks let an agency receive real-time notifications (CRM sync,
automation) when things happen in its Odisea account. An agency manages its
subscriptions under `POST/GET/DELETE /api/v1/webhooks`.

## Event catalog

All current events are **agency-scoped** — they fire to the subscriptions owned
by the agency the event belongs to.

| Event | Fired when | Payload |
|---|---|---|
| `lead.created` | A traveler submits a lead or booking request | `LeadDto` |
| `publication.published` | An agency publishes (or re-publishes) a publication | `PublicationDto` |

A subscription's `eventTypes` is a comma-separated list of these names; a
subscription only receives the events it lists.

> Operator-scoped events (e.g. `offer.published`) are not yet available — they
> need an operator-subscription model. Tracked as a follow-up.

## Delivery

- **Method**: `POST` to the subscription's `url`, `Content-Type: application/json`.
- **Body**: an envelope —
  ```json
  {
    "event": "lead.created",
    "occurredAt": "2026-06-13T10:00:00Z",
    "data": { /* the payload DTO */ }
  }
  ```
- **Best-effort**: delivery is fire-and-forget. A non-2xx response or a transport
  error is logged and dropped — it never affects the request that triggered the
  event. (A delivery log + retry policy is a planned enhancement.)
- **Timeout**: 5 seconds per delivery.

## Verifying the signature

Every delivery carries an HMAC-SHA256 signature of the **raw request body**,
keyed with the subscription's `secret` (returned once, at creation):

```
X-Odisea-Signature: sha256=<hex>
```

Recompute it on your side and compare in constant time. Node example:

```js
import { createHmac, timingSafeEqual } from 'node:crypto';

function verify(rawBody, header, secret) {
  const expected = 'sha256=' + createHmac('sha256', secret).update(rawBody).digest('hex');
  const a = Buffer.from(header);
  const b = Buffer.from(expected);
  return a.length === b.length && timingSafeEqual(a, b);
}
```

Always verify against the **raw** body bytes, before any JSON re-serialization.

## Managing subscriptions

| Verb | Route | Notes |
|---|---|---|
| `POST` | `/api/v1/webhooks` | Create. Returns the `secret` **once**. Body: `{ url, eventTypes }` |
| `GET` | `/api/v1/webhooks` | List the agency's subscriptions (never echoes secrets) |
| `POST` | `/api/v1/webhooks/{id}/disable` | Stop deliveries without deleting |
| `POST` | `/api/v1/webhooks/{id}/enable` | Resume |
| `DELETE` | `/api/v1/webhooks/{id}` | Remove |

All management routes require an `AgencyAdmin` token.
