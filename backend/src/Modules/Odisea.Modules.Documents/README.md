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
- Endpoints: operator — `/api/v1/company-profile`, `/api/v1/documents` (+`/invoices`, `/credit-notes`, `/{id}/annul`, `/{id}/pdf`); agency — `/api/v1/agency/documents` (**own documents only**, scoped by the JWT claim)
- Persistence: `DocumentsDbContext` → schema `documents`

## Relations

Consumes `Agencies.PublicApi` (`IAgencyLookup.GetBillingInfoAsync`) and `Booking.PublicApi` (`IBookingLookup`). Nothing depends on this module yet.

## Out of scope (v1) / ⚠ before production

Not implemented: payment documents, accounting ledgers/дневници по ЗДДС, VAT return exports, протоколи по чл.117, debit notes flow (enum exists, no endpoint), multi-line documents.

> ⚠ The legal mapping above is engineering's reading of the law. **Review with the company accountant/данъчен консултант before issuing real documents** — особено режима на маржа (чл.136–142) и началния номер на серията, ако фирмата вече е издавала фактури от друга система (номерацията трябва да продължи, не да започне от 1; set the `tax` counter row accordingly).
