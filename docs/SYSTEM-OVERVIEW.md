# Odisea v2 — System Overview (diagrams)

> The big picture of what exists today, as of 2026-10-24. Diagrams are Mermaid and render on GitHub. For the narrative and the rules behind each box, see [ARCHITECTURE.md](ARCHITECTURE.md); for status, [ROADMAP.md](ROADMAP.md).

## 1. The whole system

Who talks to what. Solid lines exist today; dashed lines are planned or undecided.

```mermaid
flowchart TB
    subgraph Clients
        portalA["Angular portal - agency area<br/>login, search, booking wizard, bookings, documents"]
        portalB["Angular portal - admin area<br/>(placeholders today, see issue 145)"]
        partner["External agency systems<br/>same REST API with X-Api-Key"]
    end

    subgraph Host["Odisea.Api - the single host"]
        pipeline["Rate limiting -> Authentication (JWT or API key) -> Authorization policies -> Controllers"]
        migrator["StartupMigrator<br/>migrates six schemas, then seeds (before the server listens)"]
    end

    subgraph Modules["Modules (talk to each other only through PublicApi)"]
        AG["Agencies<br/>identity, JWT, API keys, agencies, users, roles, key ring"]
        CAT["Catalog<br/>destinations, hotels, programs, departures, provider mappings, live search"]
        PRC["Pricing<br/>rules (markup, fee, commission), exchange rates, pricing engine"]
        BKG["Booking<br/>reservation lifecycle, passengers, payments, operator oversight"]
        INT["Integrations<br/>IReservationProvider, Mock adapter, offer envelope protection, call logs"]
        DOC["Documents<br/>Bulgarian invoices and credit notes, gapless numbering, PDFs, company profile"]
    end

    subgraph DB["PostgreSQL - one database, one schema per module"]
        s1[(agencies)]
        s2[(catalog)]
        s3[(pricing)]
        s4[(booking)]
        s5[(integrations)]
        s6[(documents)]
        s7[(notifications)]
    end

    subgraph Providers["External reservation systems (tier 3)"]
        MOCK["Mock provider (dev, deterministic)"]
        REAL["TourVisio / Sejour (Phase 5, undecided)"]
    end

    portalA -->|HTTPS, JWT| pipeline
    portalB -.->|HTTPS, JWT| pipeline
    partner -->|HTTPS, X-Api-Key| pipeline
    pipeline --> AG & CAT & PRC & BKG & INT & DOC
    AG --- s1
    CAT --- s2
    PRC --- s3
    BKG --- s4
    INT --- s5
    DOC --- s6
    INT --> MOCK
    INT -.-> REAL
```

## 2. Module dependencies

Each arrow is a `PublicApi` contract one module consumes from another. Everything also references `SharedKernel` (`Entity`, `Money`, `DateRange`, `Pax`, `IClock`, `ICurrentUser`, `DomainException`), and the host wires all six modules. An architecture test fails the build if any module reaches past another module's `PublicApi`.

```mermaid
flowchart LR
    Agencies["Agencies<br/>PublicApi: AuthPolicies, AuthClaims,<br/>ApiKeyAuthenticationDefaults, IAgencyLookup"]
    Catalog["Catalog<br/>PublicApi: IProgramLookup"]
    Pricing["Pricing<br/>PublicApi: IPriceCalculator, PriceBreakdown"]
    Integrations["Integrations<br/>PublicApi: IReservationProvider, IProviderRegistry,<br/>IOfferTokenProtector, OfferEnvelope, ProviderPassenger"]
    Booking["Booking<br/>PublicApi: IBookingLookup"]
    Documents["Documents<br/>(no PublicApi consumers yet)"]

    Catalog -->|IProviderRegistry, IOfferTokenProtector| Integrations
    Catalog -->|IPriceCalculator| Pricing
    Booking -->|IProviderRegistry, IOfferTokenProtector| Integrations
    Booking -->|IPriceCalculator| Pricing
    Booking -->|IProgramLookup| Catalog
    Booking -->|IAgencyLookup| Agencies
    Documents -->|IBookingLookup| Booking
    Documents -->|IAgencyLookup billing| Agencies
    Catalog & Pricing & Booking & Documents -->|AuthPolicies, AuthClaims| Agencies
```

Cross-module references in data are plain `Guid`s. No module shares an entity or a foreign key with another.

## 3. Request pipeline: who gets in, and where

```mermaid
flowchart LR
    R([Request]) --> RL["Rate limiter<br/>auth endpoints: 10 per minute per IP<br/>X-Api-Key present: 60 per minute per key (hash-partitioned)"]
    RL --> SEL{"X-Api-Key<br/>header?"}
    SEL -->|yes| AK["ApiKey handler<br/>SHA-256 lookup, key active, agency active"]
    SEL -->|no| JWT["JWT bearer<br/>HS256, 15-minute access token,<br/>HttpOnly refresh cookie with rotation"]
    AK --> CL["Claims<br/>sub, name, user_type, agency_id, role, auth_method"]
    JWT --> CL
    CL --> AZ{"Policy on the<br/>controller / action"}
    AZ -->|"Agency<br/>user_type = agency"| A["Agency surface<br/>/search, /catalog/programs,<br/>/bookings, /agency/documents"]
    AZ -->|"Operator<br/>user_type = operator"| O["Operator surface<br/>/agencies, /agencies/:id/users, /agencies/:id/api-keys,<br/>/destinations, /hotels, /programs,<br/>/pricing-rules, /exchange-rates,<br/>/documents, /company-profile, /admin/bookings"]
    AZ -->|"OperatorAdmin<br/>user_type = operator and role = Admin"| OA["Admin decisions<br/>confirm a paid reservation,<br/>cancel it, void a payment,<br/>operator branding"]
    AZ -->|AllowAnonymous| AN["/auth/login, /auth/refresh,<br/>/auth/logout, /health,<br/>/branding, /branding/logo"]
```

Two facts hold everywhere: the agency id always comes from the `agency_id` claim, never from the request, and the provider's net cost is only ever carried by `Operator*` DTOs (a guard test scans every other DTO).

## 4. Reservation lifecycle

Who may trigger each transition is in parentheses. `Rejected`, `Expired`, `Cancelled` and `Failed` are terminal.

```mermaid
stateDiagram-v2
    [*] --> Draft: create draft (agency or API key) - occupancy from the offer envelope
    Draft --> PriceConfirmed: re-price (agency)
    PriceConfirmed --> PriceConfirmed: re-price again (agency)
    Draft --> Cancelled: cancel (agency)
    PriceConfirmed --> Cancelled: cancel (agency)
    PriceConfirmed --> PendingConfirmation: submit (agency) - completeness check, saved before the provider call
    PendingConfirmation --> AwaitingPayment: provider holds the reservation (system) - PaymentDueAt set
    PendingConfirmation --> Failed: provider declines (system)
    PendingConfirmation --> Cancelled: rescue cancel (operator admin)
    AwaitingPayment --> Confirmed: confirm (operator admin) - only when fully paid
    AwaitingPayment --> Rejected: reject (operator)
    AwaitingPayment --> Expired: expire (operator) - after PaymentDueAt
    AwaitingPayment --> Cancelled: cancel (agency while unpaid, or operator admin)
    Confirmed --> Cancelled: cancel (operator admin)
    Confirmed --> [*]
    Rejected --> [*]
    Expired --> [*]
    Cancelled --> [*]
    Failed --> [*]
```

Payments are separate records (voided, never deleted); the paid amount and balance are computed. Every transition stores the actor (`Agency`, `ApiClient`, `Operator`, `System`) and the user id.

## 5. End-to-end: from search to invoice

```mermaid
sequenceDiagram
    autonumber
    actor Agent as Agency (portal user or API key)
    participant API as Odisea.Api
    participant CAT as Catalog
    participant INT as Integrations (Mock provider)
    participant PRC as Pricing
    participant BKG as Booking
    actor Ops as Operator / Operator admin
    participant DOC as Documents

    Agent->>API: POST /api/v1/search (program, departure, adults, children)
    API->>CAT: SearchAsync(agencyId from claim)
    CAT->>INT: provider.SearchAsync(external hotel codes)
    INT-->>CAT: offers with net prices (raw provider tokens)
    CAT->>PRC: CalculateSellPrice per offer
    CAT-->>Agent: sell price, commission, protected OfferEnvelope (agency, hotel, occupancy)
    Agent->>API: POST /api/v1/bookings (envelope, passengers)
    API->>BKG: CreateDraft - envelope must match agency and hotel
    Agent->>API: PUT /api/v1/bookings/{id}/passengers (complete traveller data)
    Agent->>API: POST /api/v1/bookings/{id}/re-price
    BKG->>INT: RePriceAsync(raw token)
    BKG->>PRC: CalculateSellPrice (breakdown stored as jsonb)
    BKG-->>Agent: PriceConfirmed (sell price, price-changed flag)
    Agent->>API: POST /api/v1/bookings/{id}/submit
    BKG->>BKG: EnsureReadyForSubmit, save PendingConfirmation
    BKG->>INT: BookAsync(passengers with documents)
    INT-->>BKG: provider booking reference
    BKG-->>Agent: AwaitingPayment, PaymentDueAt
    Ops->>API: POST /api/v1/admin/bookings/{id}/payments (amount, method, reference)
    Ops->>API: POST /api/v1/admin/bookings/{id}/confirm (admin role, fully paid)
    BKG-->>Ops: Confirmed (actor recorded)
    Ops->>API: POST /api/v1/documents/invoices (bookingId)
    DOC->>BKG: IBookingLookup - status, sell amount, ConfirmedAt (tax event)
    DOC-->>Ops: invoice issued (gapless number, PDF)
    Agent->>API: GET /api/v1/agency/documents
    API-->>Agent: the agency's own documents
```

## 6. Data: one database, six schemas

Solid arrows are foreign keys inside a schema. Dashed arrows are `Guid` references across schemas, deliberately without foreign keys.

```mermaid
flowchart TB
    subgraph agencies
        a1[agencies]
        a2[agency_users]
        a3[operator_users]
        a4[commission_levels]
        a5[refresh_tokens]
        a6["agency_api_keys<br/>(SHA-256 hash, prefix)"]
        a7["data_protection_keys<br/>(DataProtection key ring)"]
    end
    subgraph catalog
        c1[destinations]
        c2[hotels]
        c3[programs]
        c4[program_departures]
        c5[program_hotels]
        c6[hotel_mappings]
        c7[location_mappings]
    end
    subgraph pricing
        p1[pricing_rules]
        p2[exchange_rates]
    end
    subgraph booking
        b1["bookings<br/>(xmin concurrency token)"]
        b2["booking_passengers<br/>(document_number encrypted)"]
        b3[booking_payments]
        b4["booking_status_history<br/>(actor_type, actor_user_id)"]
        b5[/booking_ref_seq sequence/]
    end
    subgraph integrations
        i1["provider_call_logs<br/>(PII-free summaries)"]
    end
    subgraph documents
        d1[financial_documents]
        d2["document_counters<br/>(gapless numbering)"]
        d3[company_profiles]
        d4[contract_templates]
        d5[generated_contracts]
    end
    subgraph notifications
        n1["email_messages<br/>(outbox: status, attempts, next_attempt_at)"]
    end

    a2 --> a1
    a6 --> a1
    a1 -.->|commission_level_id| a4
    c3 --> c1
    c2 --> c1
    c4 --> c3
    c5 --> c3
    c5 --> c2
    c6 --> c2
    c7 --> c1
    b2 --> b1
    b3 --> b1
    b4 --> b1
    b1 -.->|agency_id, agent_user_id| a1
    b1 -.->|program_id, departure_id, hotel_id| c3
    d1 -.->|booking_id| b1
    d1 -.->|agency_id| a1
```

Conventions everywhere: snake_case names, enums stored as strings, version-7 GUID keys, a separate `__ef_migrations_history` table per schema, migrations per module under `Infrastructure/Data/Migrations/`.

## 7. Endpoint map by audience

| Audience | Endpoints | Notes |
|---|---|---|
| Anonymous | `POST /api/v1/auth/login`, `POST /api/v1/auth/refresh`, `POST /api/v1/auth/logout`, `GET /health`, `GET /api/v1/branding`, `GET /api/v1/branding/logo` | auth endpoints rate limited 10/min per IP; branding reads are public and cacheable |
| Agency (portal user or API key) | `GET /api/v1/catalog/programs`, `GET /api/v1/catalog/programs/{id}`, `POST /api/v1/search`, `POST/GET /api/v1/bookings`, `GET /api/v1/bookings/{id}`, `PUT /api/v1/bookings/{id}/passengers`, `POST /api/v1/bookings/{id}/re-price`, `POST /api/v1/bookings/{id}/submit`, `POST /api/v1/bookings/{id}/cancel`, `GET /api/v1/agency/documents[/{id}[/pdf]]`, `GET /api/v1/agency/plan`, `GET /api/v1/agency/contract-templates`, `GET/POST /api/v1/agency/contracts`, `GET /api/v1/agency/contracts/{id}/file` | scoped by the `agency_id` claim; keys additionally limited to 60/min |
| Operator | `/api/v1/agencies` (+ `/{id}/users`, `/{id}/api-keys`, `/{id}/billing`, `/{id}/plan` admin-only), `/api/v1/destinations`, `/api/v1/hotels`, `/api/v1/programs`, `/api/v1/pricing-rules`, `/api/v1/exchange-rates`, `/api/v1/documents` (invoices, credit notes, annul, pdf), `/api/v1/company-profile`, `/api/v1/contract-templates` (upload/replace/delete are admin-only), `/api/v1/contracts` (generate, data preview, file, share), `/api/v1/admin/emails` (outbox; retry, cancel, dispatch, test are admin-only), `GET /api/v1/admin/bookings[/{id}]`, `POST /api/v1/admin/bookings/{id}/reject`, `/expire`, `/payments` | back-office; sees net cost and breakdown |
| Operator admin | `POST /api/v1/admin/bookings/{id}/confirm`, `/cancel`, `/payments/{paymentId}/void`, `PUT /api/v1/branding`, `PUT/DELETE /api/v1/branding/logo` | irreversible money decisions, white-label branding |

## 8. How a change ships

```mermaid
flowchart LR
    issue["Issue on the board<br/>(Odisea - Roadmap)"] --> branch["Branch<br/>type/short-description"]
    branch --> impl["Implement with tests<br/>unit + architecture (EF InMemory)<br/>HTTP integration (real Postgres)"]
    impl --> smoke["Live smoke<br/>API against the compose db"]
    smoke --> pr["Pull request<br/>Conventional Commits"]
    pr --> ci["CI<br/>build (warnings = errors), tests with a Postgres service, CodeQL"]
    ci --> merge["Merge to main"]
    merge --> done["Board item Done"]
```

Test counts at the time of writing: 194 unit and architecture tests, 24 HTTP integration tests.

## What is not here yet

- Portal: the booking wizard still calls the retired `/confirm` route and collects only names; the admin area is placeholders (issue 145).
- A real reservation provider behind `IReservationProvider` (Phase 5, gated on the TourVisio-vs-Sejour decision and sandbox credentials).
- Automatic expiry of unpaid reservations, partner webhooks, idempotency keys for partner retries, audit log, vouchers, credit-limit enforcement, encrypted key ring at rest (issues 150 to 154, #100).
