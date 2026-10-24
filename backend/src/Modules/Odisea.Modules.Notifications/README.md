# Odisea.Modules.Notifications

**Outbound e-mail for the whole platform**, as a transactional outbox: other modules queue a message (inquiry received, auto-reply to a customer, later booking status mails), a background dispatcher sends it, and operators can see and retry what went out. No API request ever waits on an SMTP server.

## Owns

- `EmailMessage` — one queued mail: recipient, reply-to, subject, text + optional HTML body, `Category` (machine tag, e.g. `inquiry-notification`), `AgencyId`, `CorrelationId` (the submission id / booking ref that caused it), `Status` (`Pending` → `Sent` | `Failed` | `Cancelled`), `Attempts`, `NextAttemptAt`, `LastError`.
- `EmailOutbox` (`IEmailOutbox` in `PublicApi`) — validates and stores. Plain mailbox addresses only (`name@host`).
- `EmailDispatchRun` — one pass: due pending messages in `NextAttemptAt` order, batch of `Email:BatchSize`, one save per message. Failures back off 1 min → 5 min → 30 min → 2 h; after `Email:MaxAttempts` the message parks as `Failed` until an operator retries.
- `EmailDispatcher` — hosted service polling every `Email:PollSeconds` (`Email:Enabled=false` turns it off; tests do that and call `POST /dispatch`).
- Transports: `SmtpEmailTransport` (MailKit; STARTTLS by default, implicit TLS when `UseStartTls=false`) when `Email:Smtp:Host` is set, otherwise `LoggingEmailTransport` (development/tests: logs the recipient's *domain* only and marks the mail sent).
- Endpoints (operator): `GET /api/v1/admin/emails?status&agencyId&category`, `GET /{id}` (with bodies), `POST /{id}/retry`; admin only: `POST /{id}/cancel`, `POST /dispatch` (flush now), `POST /test {to}` (verifies the SMTP setup end to end).
- Persistence: `NotificationsDbContext` → schema `notifications`.

## Configuration

```json
"Email": {
  "Enabled": true,
  "From": "noreply@operator.example",
  "FromName": "Operator name",
  "Smtp": { "Host": "", "Port": 587, "Username": "", "Password": "", "UseStartTls": true },
  "PollSeconds": 5, "BatchSize": 20, "MaxAttempts": 5
}
```

`Smtp:Host`, `Username`, `Password` and `From` belong in user-secrets / environment (`Email__Smtp__Password`), never in committed files. `From` is required once a host is set (validated at startup).

## Relations

Depends on `Agencies.PublicApi` (authorization policies) and `SharedKernel`. Consumed through `Notifications.PublicApi.IEmailOutbox` by Workspace (inquiry notifications, auto-replies); Booking may follow.

## Limits / later

Single API instance assumed (no row claiming across instances). No attachments, no templates engine (callers render bodies), no bounce handling, no per-agency sender identities — the operator's `From` is used for everything.
