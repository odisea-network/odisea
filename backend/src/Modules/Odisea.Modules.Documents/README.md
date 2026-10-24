# Odisea.Modules.Documents

**Данъчните документи.** Issues фактури and кредитни/дебитни известия to agencies per Bulgarian legislation, renders them as Bulgarian-language PDFs, and never lets a non-compliant document out.

## Legal mapping (what the code enforces, and where)

| Rule | Legal basis | Where in code |
|---|---|---|
| 10-digit, ascending, **gapless** document numbers | чл.114, ал.1, т.2 ЗДДС; чл.78 ППЗДДС | `NumberSeries` — atomic counter upsert INSIDE the issuing transaction (a plain sequence would leave gaps on rollback); `DocumentService.PersistNumberedAsync` |
| Mandatory requisites (издател/получател: наименование, ЕИК, ИН по ЗДДС, адрес; описание; дата на данъчно събитие; данъчна основа/ставка/данък/сума) | чл.114, ал.1 ЗДДС; чл.6 ЗСч | `DocumentService.RequireCompanyProfileAsync` / `RequireBillingAsync` — issuing is **blocked** with a clear message when requisites are missing; all values snapshotted onto `FinancialDocument` |
| Margin scheme for tour operators: VAT **not** shown separately + mandatory wording "режим на облагане на маржа — туристически услуги" | чл.136–142 ЗДДС (text: чл.142, ал.1); чл.86, ал.1 ППЗДДС | `VatCalculator.MarginSchemeNote`; `VatTreatment.MarginScheme` is the **default** for bookings |
| Rates: 20% standard, 9% настаняване, exempt with stated grounds | чл.66, чл.66а ЗДДС; grounds: чл.114, ал.1, т.12 | `VatCalculator.FromGross` — base backed out of the gross, base+VAT always reconstructs the total exactly |
| Credit/debit notes reference the original invoice (number + date) | чл.115, ал.4 ЗДДС | `IssueCreditNoteAsync` — also requires the booking to actually be cancelled (чл.115, ал.1) |
| Wrong documents are **annulled and kept**, never deleted or edited | чл.116 ЗДДС | `FinancialDocument.Annul`; a booking becomes re-invoiceable only after annulment |
| Invoice within 5 days of the tax event | чл.113, ал.4 ЗДДС | Late issuing is logged as a warning (must still be possible), both dates printed on the document |
| No signature/stamp required on the document | чл.114 ЗДДС / чл.6 ЗСч practice | PDF footer note |
| Retention | 10 години (чл.38 ДОПК) | Documents are immutable rows; nothing in the API deletes them |

## Owns

- `FinancialDocument` (full supplier/recipient/amount **snapshots** — later edits to agencies or the company profile never touch issued documents), `CompanyProfile` (издателя, operator-managed singleton), `DocumentCounter`
- PDF rendering (`DocumentPdfRenderer`): QuestPDF + embedded DejaVu Sans (Cyrillic), ФАКТУРА/ИЗВЕСТИЕ layout with доставчик/получател, totals, legal note, payment details, АНУЛИРАН watermark text
- Endpoints: operator — `/api/v1/company-profile`, `/api/v1/documents` (+`/invoices`, `/credit-notes`, `/{id}/annul`, `/{id}/pdf`), `/api/v1/contract-templates`, `/api/v1/contracts`; agency — `/api/v1/agency/documents`, `/api/v1/agency/contract-templates`, `/api/v1/agency/contracts` (**own documents only**, scoped by the agency claim)
- Contracts: `ContractTemplate` (operator-uploaded `.docx` with `{{Placeholders}}`, versioned), `GeneratedContract` (the filled file, immutable), `DocxTemplateRenderer`, `ContractMergeDataBuilder`, `ContractService` — see below
- Persistence: `DocumentsDbContext` → schema `documents`

## Relations

Consumes `Agencies.PublicApi` (`IAgencyLookup.GetBillingInfoAsync`, `GetContractInfoAsync`) and `Booking.PublicApi` (`IBookingLookup.FindAsync`, `GetContractInfoAsync` — booking facts plus passengers for contracts). Nothing depends on this module yet.

## Out of scope (v1) / ⚠ before production

Not implemented: payment documents, accounting ledgers/дневници по ЗДДС, VAT return exports, протоколи по чл.117, debit notes flow (enum exists, no endpoint), multi-line documents.

> ⚠ The legal mapping above is engineering's reading of the law. **Review with the company accountant/данъчен консултант before issuing real documents** — особено режима на маржа (чл.136–142) и началния номер на серията, ако фирмата вече е издавала фактури от друга система (номерацията трябва да продължи, не да започне от 1; set the `tax` counter row accordingly).

## Contracts from Word templates

Operators need contracts as Word documents (agency agreements, travel contracts for a booking's travellers). The module fills operator-authored `.docx` templates; no Word or LibreOffice on the server (Open XML SDK only).

**Authoring a template.** Any `.docx` (max 5 MB, macro-enabled files are rejected). Write `{{Placeholder}}` where a value goes — in the body, headers, footers, footnotes, text boxes. Names are case-insensitive and may be split across Word runs by spell-check or formatting; the renderer matches on the paragraph's joined text and writes the value into the run where the token starts, so the surrounding formatting survives. A value with line breaks becomes Word line breaks. A **table row** that contains `{{Passengers.X}}` is repeated once per traveller (`{{Passengers.#}}` is the row number); an empty list removes the row.

**What the API fills** — `GET /api/v1/contract-templates/placeholders` lists it: `Today`, `Year`; `Company.*` (company profile); `Agency.*` (name, country, status, contact e-mail, credit limit, legal name, ЕИК, VAT number, address, city, МОЛ); `Booking.*` (ref, status, program, hotel, check-in/out, nights, adults/children/pax, sell price, commission, currency, paid, balance due, payment deadline, created/confirmed dates); `Lead.*` (the lead traveller); `Passengers.Count`, `Passengers.Names`; per-row `Passengers.*` (full name, title, names, gender, birth date, type at check-in, nationality, document type/number/issuing country/expiry, e-mail, phone). Dates print as `dd.MM.yyyy`, amounts as `1 036,38`, times in Europe/Sofia. **Net cost is never available** to a template.

**Custom fields.** Any placeholder outside the catalogue (e.g. `{{SpecialTerms}}`) is a *custom field*: the upload response lists them (`customFields`) and generation fails with 422 naming the unfilled ones until they are supplied in `fields`. Catalogue placeholders cannot be overridden through `fields` — a contract must not state a price other than the booking's.

**Generating.** `POST /api/v1/contracts {templateId, bookingId?, agencyId?, fields?, note?}` → the filled file is stored as a `GeneratedContract` with the template version used, who generated it and for which booking/agency; `GET /{id}/file` downloads it. `GET /api/v1/contracts/data?bookingId&agencyId` previews the values a template would receive. Generated contracts are immutable (they are the record of what was handed out); a template used by a contract cannot be deleted — deactivate it (`PUT /{id}` with `isActive: false`). Replacing the file (`PUT /{id}/file`) bumps the version and re-discovers placeholders.

**Agencies.** They see only contracts the operator **shared** (`POST /api/v1/contracts/{id}/share`, `/unshare`) under `GET /api/v1/agency/contracts[/{id}[/file]]`, scoped by their `agency_id` claim. Templates marked `agencyUsable` appear under `GET /api/v1/agency/contract-templates`, and `POST /api/v1/agency/contracts {templateId, bookingId, fields?}` lets an agency (portal user or API key) generate for **its own** bookings — anything outside its reach is a 404; such contracts are shared at once and flagged `generatedByAgency`; at most 20 agency-generated contracts per booking.

**Roles.** Every operator reads templates and generates; uploading, replacing, editing and deleting templates needs `OperatorAdmin` (it is the legal text).

**Storage & logs.** Files live in Postgres (`documents.contract_templates`, `documents.generated_contracts`, `bytea`). Logs carry ids, names, sizes and counts — never placeholder values. Not included: PDF export of contracts (needs a converter service), e-signature, template localisation of enum values (Gender, document type print as `Male`/`Passport` until the business templates define the wording).
