---
name: create-program
description: Turn an operator's brief (notes, brochure text, hotel list, dates, policies, photos) into a complete, sell-ready Odisea program through the API — content, itinerary, departures, hotels, images — and report what the readiness checklist still wants. Use when the user says "create a program", "add a new program / offer / package", "load this brochure as a program", or hands over trip material to put on sale.
---

# Create a program from a brief

A program is what agencies sell and what a customer reads before paying. The API keeps the
rules: `POST /api/v1/programs/{id}/publish` refuses a program until the **required** checks pass,
and `GET /api/v1/programs/{id}/readiness` lists every check, required and recommended, with a
score. Your job is to turn whatever the operator hands over into a spec that passes all of them,
without inventing a single fact.

Read [reference/content-guide.md](reference/content-guide.md) before writing; it says what each
section is for, how long it is, and the checklist the API applies. The spec file format is in
[reference/spec-format.md](reference/spec-format.md); a complete example is
[examples/antalya-easter-2027.json](examples/antalya-easter-2027.json).

## Steps

### 1. Collect the facts

Read everything the user gives you: pasted notes, PDF brochures (`Read` with `pages`), hotel
lists, supplier confirmations, photos in a folder. Pull out, verbatim where it matters:

- destination (city/resort + ISO country code), season or occasion, year
- each departure: start date, end date, transport note (route, carrier, times if known)
- hotels: names, stars, supplier codes if the brief has them
- transport: mode, route, baggage, departure point, times
- what is included and what is not — exactly as the operator states it
- cancellation, payment, insurance and child terms — exactly as the operator states them
- the reservation provider code (`mock` on the dev stack; ask otherwise)
- image files: paths, which one is the cover

**Essentials you must have** before creating anything: destination + country, at least one
departure with both dates, at least one hotel, the transport mode, the cancellation policy,
the provider code. If any are missing, ask for all of them in **one** question. Everything else
(tagline, summary, highlights, practical info, itinerary wording) you write from the facts.

### 2. Write the spec

Write `<scratchpad>/program-spec.json` in the format of `reference/spec-format.md`, following
the content guide section by section. Language: the language of the brief unless the user says
otherwise (Bulgarian briefs produce Bulgarian content; ISO dates and codes stay as they are).

Then check your own spec against the readiness table at the end of the content guide. Fix
before importing — a 422 from the API is a sign you skipped this step.

### 3. Dry run

```powershell
pwsh .claude/skills/create-program/scripts/Import-ProgramSpec.ps1 -Spec <path> -DryRun
```

Validates the file (dates, lengths, image files, slug shape) and prints what would be created.
Show the user that plan — name, destination, departures, hotels (and which would be *created* as
new catalogue entries), image count — and wait for a go-ahead. New destinations and hotels are
master data every operator sees, so never create them silently.

### 4. Import

```powershell
pwsh .claude/skills/create-program/scripts/Import-ProgramSpec.ps1 -Spec <path> `
  -BaseUrl http://localhost:8090 -Email admin@odisea.local -Password '<dev password>'
```

(Or `-Token <jwt>` when the user already has one. Never write a password into the spec or a
committed file.) The script resolves or creates the destination and hotels, creates the program
with its whole content block in one call, adds departures, attaches hotels, uploads images, and
prints the readiness report: score, the required checks, and every recommended check still open.

### 5. Finish

- Work through the open checks you can fix from the brief (alt text, a missing day, tags).
- Items that need the operator — supplier mappings for hotels, a real cover photo, the standard
  insurance text — go in your hand-back as a short list with what is needed for each.
- Publish only when the user asks: `-Publish` on the script or `POST /api/v1/programs/{id}/publish`.

Hand back: program id, slug, status, readiness score, open checks with what each needs, and the
operator URL (`<BaseUrl>/swagger` on the dev stack; the admin page once the portal has one).

## Rules

- **Never invent facts.** Dates, hotels, carriers, baggage allowances, cover amounts, cancellation
  tiers and prices come from the operator or they stay out. Where a policy is missing, ask for the
  operator's standard text; do not paste a plausible template and present it as theirs.
- **No prices in the content.** Prices are live from the pricing engine at search time; the text
  describes what is bundled, never what it costs.
- **Images** come from the user (JPEG, PNG or WebP, ≤ 5 MB, cover ≥ 1200 px wide, landscape).
  Do not pull pictures from the web unless the user gives the URL and confirms the rights.
- **Names** follow `<Destination> <Occasion or Season> <Year>` unless the operator has a scheme;
  10–70 characters, no capitals-for-emphasis, no exclamation marks.
- **Slug**: leave it to the API unless the user wants a specific one.
- **Tags** come from the vocabulary in the content guide; add a new one only when nothing fits,
  and say so in the hand-back.
- One program per spec. A brief with several products (two destinations, summer and winter
  seasons with different hotels) is several specs, run one at a time.
