# Program content guide

What each section of a program is for, how long it should be, and what makes it good. Field
names match `SaveProgramContentRequest` in the API and the spec file. Limits are enforced by the
API; targets are what a finished product page looks like.

## Tone and language

- Write in the language of the brief. Keep place names, hotel names, airport codes and
  supplier codes as the operator writes them.
- Plain, concrete sentences. Numbers with units (`20 kg`, `1 h 45 min`, `10 000 EUR`). Dates in
  the text as `30.03.2027`; dates in the spec's `departures` as ISO `2027-03-30`.
- No superlatives without a fact behind them ("500 m from the beach" beats "fantastic location").
- Second person sparingly ("you choose the hotel at booking"), never hype ("you'll love it!").
- Markdown fields (`description`, `transportDetails`, `practicalInfo`, the four policies,
  itinerary `description`): headings `##`/`###`, bullet lists, **bold** for labels. No HTML, no
  images, no links to competitors.
- Never state a price. Prices are computed live per agency.

## Sections

| Field | Limit | Target | What it is for |
|---|---|---|---|
| `name` | 200 (readiness wants 10–70) | `<Destination> <Occasion/Season> <Year>` — "Antalya Easter 2027" | The product's title in every list and document |
| `season` | 50 | "Easter 2027", "Summer 2027" | Grouping in the operator's catalogue |
| `content.tagline` | 160 | One line: duration + board + transport — "7 nights all-inclusive with a direct charter from Sofia" | Under the title on cards and the detail page |
| `content.summary` | 600 (readiness ≥ 50) | 150–400 chars, 2–3 sentences: where, how long, what is bundled, who it suits | Card text, search snippet, meta description |
| `description` | 20 000 (readiness ≥ 300) | 300–3000 chars of Markdown: an intro paragraph, then `### Who it is for` and `### How it works` (choose departure and hotel at booking; prices live) and optionally `### The area` / `### The hotels` | The body of the product page |
| `content.highlights` | 12 × 200 (readiness ≥ 3) | 3–6 bullets, each a specific fact: route and flight time, named hotels, named excursions, what is already in the price | The "why this one" list |
| `content.inclusions` | 40 × 300 (required ≥ 1) | Exhaustive, one item per line, each precise (baggage kg, board basis, transfer type, insurance type and cover). Order: transport, accommodation, transfers, insurance, services, taxes | What the customer is paying for |
| `content.exclusions` | 40 × 300 (readiness ≥ 1) | What people assume is included but is not: optional excursions and entrance fees, personal expenses, optional insurance, visas, seat selection, taxes paid locally | Prevents complaints |
| `content.tags` | 15 × 40, kebab-case (readiness ≥ 1) | 1–6 from the vocabulary below | Filtering on agency sites |
| `content.transportMode` | enum | `Flight`, `Bus`, `Train`, `Ship`, `OwnTransport`, `Mixed` (never leave `Unspecified`) | Drives the transport section |
| `content.transportDetails` | 4000 | Markdown: carrier if known, route with IATA codes, outbound/return times marked **indicative**, baggage per person, when times are confirmed. Bus: pickup points and times. Own transport: meeting point and directions | The "getting there" block |
| `content.departurePoint` | 200 | "Sofia Airport (SOF), Terminal 2" / "Sofia, Central Bus Station, platform 4" | Shown on cards and vouchers |
| `content.minGroupSize` / `maxGroupSize` | 1–999 | Only for escorted or group tours; otherwise omit | Tells agencies when a departure may be cancelled for low numbers |
| `itinerary` | 60 days | One entry per day, **days = nights + 1** of the departure; `title` ≤ 8 words; `description` 1–3 sentences; `meals` flags; `overnight` place. Optional excursions carry "(optional)" in the title | The day-by-day plan |
| `content.practicalInfo` | 8000 | Markdown bullets with bold labels: **Documents** (passport/ID validity, visa per nationality the operator sells to), **Currency**, **Weather** for the season, **Time zone**, **Health**, **What to pack**, **Local customs** where relevant | Reduces pre-departure questions |
| `content.cancellationPolicy` | 8000 (required) | The operator's tiers: days before departure → % retained or fee; no-show; name changes | Legal text the customer accepts |
| `content.paymentTerms` | 4000 | Deposit %, when the balance is due, what happens inside the deadline, accepted methods | Same |
| `content.insurancePolicy` | 4000 | What is included (type, cover amount), what is optional, exclusions the operator states | Same |
| `content.childPolicy` | 2000 | Age bands, sharing rules, infants, on which date ages are counted | Same |
| `images` | 20 × 5 MB, JPEG/PNG/WebP | 3–8 pictures; the cover ≥ 1200 px wide, landscape; `altText` on every one describing the picture ("Sandy beach with calm sea at sunrise"), not the program; `caption` optional and short | Gallery and cards |

Policies come from the operator **verbatim**. If the brief has none, ask — a policy you wrote is
a contract the operator never agreed to.

## Tag vocabulary

Board: `all-inclusive`, `ultra-all-inclusive`, `half-board`, `full-board`, `bed-and-breakfast`,
`self-catering`.
Audience: `family`, `couples`, `seniors`, `groups`, `solo-friendly`.
Type: `beach`, `city-break`, `ski`, `spa`, `cruise`, `excursion`, `cultural`, `pilgrimage`,
`exotic`, `nature`, `wine-and-food`.
Occasion: `christmas`, `new-year`, `easter`, `may-holidays`, `summer`, `autumn`, `winter`.
Transport: `charter`, `scheduled-flight`, `bus`, `own-transport`.
Sales: `early-booking`, `last-minute`, `guaranteed-departure`.

## Readiness checklist (what the API checks)

Required — publishing is refused until all pass:

| Code | Check |
|---|---|
| `departures` | at least one departure that has not started |
| `hotels` | at least one allowed hotel |
| `summary` | summary of at least 50 characters |
| `cover` | a cover image |
| `inclusions` | at least one inclusion |
| `cancellation` | cancellation policy present |

Recommended — reported with the score, do not block:

| Code | Check |
|---|---|
| `title` | name 10–70 characters |
| `tagline` | tagline present |
| `description` | description of at least 300 characters |
| `highlights` | at least 3 highlights |
| `exclusions` | at least one exclusion |
| `itinerary` | as many days as the first departure has (nights + 1) |
| `transport` | mode set and details given |
| `departure-point` | departure point present |
| `transport-notes` | every departure has a transport note |
| `mappings` | every allowed hotel has a supplier mapping for the program's provider |
| `practical` | practical information present |
| `payment` | payment terms present |
| `insurance` | insurance policy present |
| `children` | child policy present |
| `gallery` | at least 3 images |
| `alt-text` | alt text on every image |
| `cover-size` | cover at least 1200 px wide |
| `tags` | at least one tag |

Aim for 100. Items that depend on the operator (supplier mappings, real photos, standard policy
texts) are reported in the hand-back, not guessed.
