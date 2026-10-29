# Odisea.Modules.Catalog

**What we sell.** The operator's product master data: where we go, which hotels we work with, and which packaged programs agencies can book — with everything a customer reads before paying. *(Entities + operator CRUD + mappings: implemented. Search implemented: agencies browse published programs and get live sell-priced offers; provider tokens are DataProtection-wrapped so net costs never leave the backend. Program content, images and readiness checks: implemented.)*

## Owns

- **Domain:** `Destination`, `Hotel` (internal master records), `Program` (season, destination, provider, slug, Draft→Published→Archived), `ProgramContent` (owned by the program: tagline, summary, highlights, inclusions, exclusions, tags, transport, group size, practical info, cancellation / payment / insurance / child policies), `ProgramItineraryDay` (day-by-day plan with meals and overnight), `ProgramImage` (gallery with cover, caption, alt text, pixel size), `ProgramDeparture` (date range, transport), `ProgramHotel` (the allowed-hotels list per program)
- **Readiness:** `ProgramReadiness.Evaluate` is a pure checklist over a program, its images and its hotel mappings. Six **required** checks (upcoming departure, allowed hotel, summary ≥ 50 chars, cover image, at least one inclusion, cancellation policy) gate `Program.Publish`; eighteen **recommended** checks (title length, tagline, description ≥ 300 chars, ≥ 3 highlights, exclusions, itinerary covering every day, transport mode + details, departure point, transport note per departure, supplier mappings, practical info, payment / insurance / child policies, ≥ 3 images, alt text everywhere, cover ≥ 1200 px, tags) feed a 0–100 score. `POST /programs/{id}/publish` answers 422 naming the required checks that still fail.
- **Mappings:** `HotelMapping` / `LocationMapping` — the translation table between OUR ids and each external provider's codes. Catalog owns this because hotels are our master data; Integrations stays provider-generic. Unmapped provider results are dropped + logged, never shown half-mapped.
- **CRUD:** operator-only program/hotel/destination management; agency-visible published-program search support
- **Persistence:** `CatalogDbContext` → schema `catalog` (`programs` carries the content columns; `program_itinerary_days`, `program_images` with the bytes in `bytea`)

## Program content

- `POST /api/v1/programs` accepts an optional `content` block, so one call creates a finished program; `PUT /api/v1/programs/{id}/content` replaces the block and the itinerary as a whole. Lists are trimmed and bounded (12 highlights, 40 inclusions/exclusions, 15 kebab-case tags); itinerary days must run 1, 2, 3… Long texts are Markdown and are stored as written — rendering is the client's job.
- **Slug**: unique, derived from the name (Bulgarian Cyrillic transliterated, `-2`, `-3` suffixes on clashes), settable through the content request. Programs that predate slugs got `program-<8 hex>` from the migration.
- **Images**: `POST /api/v1/programs/{id}/images` (multipart `file`, `caption`, `altText`, `isCover`; JPEG/PNG/WebP, ≤ 5 MB, ≤ 20 per program). The format and pixel size are read from the bytes, never from the client's content type; SVG and GIF are refused. The first upload becomes the cover; `PUT …/images/{imageId}` edits caption/alt/cover, `PUT …/images/order` reorders, `DELETE` removes (the next image becomes cover). Only metadata is ever listed — the bytes are served by **`GET /api/v1/public/program-images/{imageId}`**, anonymous, with `Cache-Control: public, max-age=31536000, immutable` and the SHA-256 as ETag (304 on `If-None-Match`). Bytes behind an id never change; ids are unguessable; the response is the picture and nothing else. Pictures live in Postgres for now (one database, one backup); object storage + CDN is the next step when the catalogue grows.
- **Agency view**: `GET /api/v1/catalog/programs` adds slug, tagline, summary, tags and the cover URL to each card; `GET /api/v1/catalog/programs/{id}` returns the full content, itinerary and gallery (readiness is operator-only).
- The dev seed (`DemoProgram`) is a complete example: 8-day itinerary, policies, three generated 1600×900 pictures, readiness 100. The **`create-program`** skill in `.claude/skills/` turns an operator brief into a spec and runs it through these endpoints.

## PublicApi

`IProgramLookup.ResolveBookingTargetAsync` — Booking's view of a (program, departure, hotel) triple.

## Relations

Depends on SharedKernel, Agencies (policies), Integrations (provider registry, token protection) and Pricing (sell price). Pricing scopes rules by `ProgramId` (plain Guid); Booking references programs/departures/hotels by Guid; the search feature translates internal ↔ external codes before/after calling Integrations.
