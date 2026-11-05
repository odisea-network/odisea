# Program spec format

One JSON file describes one program. `scripts/Import-ProgramSpec.ps1` turns it into API calls in
this order: destination → location mapping → hotels → hotel mappings → program (with content and
itinerary in the same call) → departures → allowed hotels → images → readiness → optional publish.
Relative image paths are resolved against the spec file's folder.

```json
{
  "program": {
    "name": "Antalya Easter 2027",
    "season": "Easter 2027",
    "providerCode": "mock",
    "description": "## A week on the Turkish Riviera\n\n…Markdown…",
    "content": {
      "tagline": "7 nights all-inclusive with a direct charter from Sofia",
      "summary": "A week of early-season sun…",
      "highlights": ["…", "…", "…"],
      "inclusions": ["…"],
      "exclusions": ["…"],
      "tags": ["all-inclusive", "family"],
      "transportMode": "Flight",
      "transportDetails": "…Markdown…",
      "departurePoint": "Sofia Airport (SOF), Terminal 2",
      "minGroupSize": null,
      "maxGroupSize": null,
      "practicalInfo": "…Markdown…",
      "cancellationPolicy": "…",
      "paymentTerms": "…",
      "insurancePolicy": "…",
      "childPolicy": "…",
      "itinerary": [
        { "day": 1, "title": "Arrival in Antalya", "description": "…", "meals": "Lunch, Dinner", "overnight": "Antalya" }
      ],
      "slug": null
    }
  },
  "destination": { "name": "Antalya", "country": "TR", "providerLocationCode": "ANT" },
  "hotels": [
    { "name": "Mock Palace Resort", "stars": 5, "providerHotelCode": "MOCK-ANT-001" }
  ],
  "departures": [
    { "startDate": "2027-03-30", "endDate": "2027-04-06", "transportNote": "Charter SOF-AYT" }
  ],
  "images": [
    { "file": "images/lara-beach.jpg", "caption": "Lara beach at sunrise", "altText": "Sandy beach with a calm sea at sunrise", "isCover": true }
  ],
  "publish": false
}
```

## Fields

| Path | Type | Notes |
|---|---|---|
| `program.name` | string, 1–200 | readiness wants 10–70 |
| `program.season` | string ≤ 50 or null | |
| `program.providerCode` | string | a registered reservation provider; `mock` on the dev stack |
| `program.description` | Markdown ≤ 20 000 or null | |
| `program.content` | object | exactly `SaveProgramContentRequest`; omitted lists are empty |
| `content.transportMode` | `Unspecified` `Flight` `Bus` `Train` `Ship` `OwnTransport` `Mixed` | |
| `content.itinerary[].day` | 1…60, consecutive from 1 | the API rejects gaps and repeats |
| `content.itinerary[].meals` | `None`, or any of `Breakfast`, `Lunch`, `Dinner` comma-separated | "Breakfast, Dinner" |
| `content.slug` | kebab-case ≤ 120 or null | null lets the API derive it from the name |
| `destination.name` + `country` | string + ISO 3166-1 alpha-2 | matched case-insensitively on both; created when missing |
| `destination.providerLocationCode` | string or absent | creates the location mapping for `providerCode` when missing |
| `hotels[].name` | string | matched case-insensitively within the destination; created when missing |
| `hotels[].stars` | 1–5 | used only when the hotel is created |
| `hotels[].providerHotelCode` | string or absent | creates the hotel mapping for `providerCode` when missing |
| `departures[]` | ISO dates, `endDate` > `startDate` | `transportNote` ≤ 500 |
| `images[].file` | path, JPEG/PNG/WebP ≤ 5 MB | relative to the spec file |
| `images[].isCover` | bool | at most one `true`; otherwise the first image is the cover |
| `publish` | bool | `true` publishes after import when every required check passes |

## What the script never does

- Change an existing program. It always creates a new one; re-running a spec creates a duplicate
  with a `-2` slug. Edit through the API (`PUT /api/v1/programs/{id}/content`) instead.
- Delete or rename destinations or hotels.
- Store credentials. Pass `-Token`, or `-Email`/`-Password` on the command line.
