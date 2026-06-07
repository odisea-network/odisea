# Odisea Components — Public Contract v1

This document is the stability contract for the `od-*` web component library.
Every item listed here is promised to remain backwards-compatible within a major version.
Breaking any item below requires a new major version and a deprecation notice.

---

## Element tags

The following custom-element tags are registered by the v1 bundle and must remain defined:

| Tag | Purpose |
|---|---|
| `od-offer-card` | Single offer card with image, price, and CTA |
| `od-offer-grid` | Responsive grid of offer cards |
| `od-offer-carousel` | Horizontally scrollable carousel of offer cards |
| `od-filter-panel` | Sidebar filter panel (country, board, category, price) |
| `od-offer-details` | Full single-offer detail view |
| `od-price-table` | Tabular departure × board × occupancy price matrix |
| `od-booking-inquiry` | Booking inquiry form |
| `od-destination-hero` | Full-width hero banner for a destination |
| `od-featured-collections` | Grid of collection cards |

---

## Public attributes and properties

### `od-offer-card`

| Attribute (HTML) | Property (JS) | Type | Default | Notes |
|---|---|---|---|---|
| `offer-id` | `offerId` | `string` | `''` | Fetches offer when `offer` prop is absent |
| `api-base` | `apiBase` | `string` | `''` | Origin prefix for API calls |
| `cta-label` | `ctaLabel` | `string` | `'Виж офертата'` | CTA button label |
| `card-style` | `cardStyle` | `'default' \| 'compact' \| 'editorial'` | `'default'` | Visual variant; reflected |
| — | `offer` | `OfferDto \| null` | `null` | Direct data injection (property only) |

### `od-offer-grid`

| Attribute | Property | Type | Default |
|---|---|---|---|
| `collection` | `collection` | `string` | — |
| `publication` | `publication` | `string` | — |
| `endpoint` | `endpoint` | `string` | — |
| `api-base` | `apiBase` | `string` | `''` |
| `columns` | `columns` | `number` | — (auto-fill) |
| `card-style` | `cardStyle` | `'default' \| 'compact' \| 'editorial'` | `'default'` |
| `title` | `title` | `string` | — |
| `cta-label` | `ctaLabel` | `string` | `'Виж офертата'` |
| — | `offers` | `OfferDto[]` | — |

### `od-offer-carousel`

| Attribute | Property | Type | Default |
|---|---|---|---|
| `collection` | `collection` | `string` | — |
| `publication` | `publication` | `string` | — |
| `endpoint` | `endpoint` | `string` | — |
| `api-base` | `apiBase` | `string` | `''` |
| `card-style` | `cardStyle` | `'default' \| 'compact' \| 'editorial'` | `'default'` |
| `title` | `title` | `string` | — |
| `cta-label` | `ctaLabel` | `string` | `'Виж офертата'` |
| `card-width` | `cardWidth` | `number` | `248` |
| — | `offers` | `OfferDto[]` | — |

### `od-filter-panel`

| Attribute | Property | Type | Default |
|---|---|---|---|
| `title` | `title` | `string` | `'Филтри'` |
| `max-price` | `maxPrice` | `number` | `3000` |
| `default-max-price` | `defaultMaxPrice` | `number` | `3000` |
| — | `offers` | `OfferDto[]` | `[]` |

### `od-offer-details`

| Attribute | Property | Type | Default |
|---|---|---|---|
| `offer-id` | `offerId` | `string` | `''` |
| `api-base` | `apiBase` | `string` | `''` |
| `cta-label` | `ctaLabel` | `string` | `'Запитване'` |
| — | `offer` | `OfferDto \| null` | `null` |

### `od-price-table`

| Attribute | Property | Type | Default |
|---|---|---|---|
| `offer-price` | `offerPrice` | `number` | — |
| `currency` | `currency` | `string` | `'€'` |
| `caption` | `caption` | `string` | — |
| — | `rows` | `PriceRow[]` | — |

### `od-booking-inquiry`

| Attribute | Property | Type | Default |
|---|---|---|---|
| `offer-title` | `offerTitle` | `string` | `''` |
| `offer-id` | `offerId` | `string` | `''` |
| `heading` | `heading` | `string` | `'Запитване за оферта'` |
| `submit-label` | `submitLabel` | `string` | `'Изпрати запитване'` |

### `od-destination-hero`

| Attribute | Property | Type | Default |
|---|---|---|---|
| `image` | `image` | `string` | `''` |
| `eyebrow` | `eyebrow` | `string` | `''` |
| `title` | `title` | `string` | `''` |
| `subtitle` | `subtitle` | `string` | `''` |
| `cta-label` | `ctaLabel` | `string` | `''` |
| `min-height` | `minHeight` | `string` | `'230px'` |

### `od-featured-collections`

| Attribute | Property | Type | Default |
|---|---|---|---|
| `endpoint` | `endpoint` | `string` | — |
| `api-base` | `apiBase` | `string` | `''` |
| `title` | `title` | `string` | — |
| `columns` | `columns` | `number` | `3` |
| — | `collections` | `CollectionDto[]` | — |

---

## Public events

| Element | Event name | Detail type | Fired when |
|---|---|---|---|
| `od-offer-card` | `od-cta-click` | `{ offer: OfferDto }` | CTA button clicked |
| `od-offer-grid` | `od-cta-click` | `{ offer: OfferDto }` | Bubbles from contained card |
| `od-offer-carousel` | `od-cta-click` | `{ offer: OfferDto }` | Bubbles from contained card |
| `od-offer-details` | `od-cta-click` | `{ offer: OfferDto }` | Primary CTA clicked |
| `od-filter-panel` | `od-filter-change` | `FilterState` | Any filter changes |
| `od-booking-inquiry` | `od-inquiry-submit` | `InquiryPayload` | Form submitted |
| `od-destination-hero` | `od-hero-cta` | `void` | Hero CTA clicked |
| `od-featured-collections` | `od-collection-click` | `{ collection: CollectionDto }` | Collection card clicked |

All events are dispatched with `bubbles: true, composed: true` so they cross shadow-DOM boundaries.

---

## CSS parts (`::part()`)

### `od-offer-card`
`card` · `image` · `body` · `title` · `price` · `cta` · `tag` · `meta`

### `od-offer-grid`
`grid` · `head` · `loading` · `empty` · `error`

### `od-offer-carousel`
`track` · `head` · `nav-prev` · `nav-next` · `loading` · `empty` · `error`

### `od-filter-panel`
`panel` · `heading` · `group`

### `od-offer-details`
`details` · `image` · `body` · `title` · `price` · `meta` · `cta`

### `od-price-table`
`table` · `head` · `row` · `best` · `price`

### `od-booking-inquiry`
`form` · `heading` · `field` · `submit` · `success-msg`

### `od-destination-hero`
`hero` · `inner` · `eyebrow` · `title` · `subtitle` · `cta`

### `od-featured-collections`
`grid` · `head` · `card` · `image` · `label` · `count`

---

## CSS custom properties (`--odc-*`)

These properties are the theming surface. All `od-*` elements read them from `:host`
with sane fallbacks. Agencies override them on the element or a common ancestor.

| Property | Purpose | Default |
|---|---|---|
| `--odc-font` | Body / UI font stack | `system-ui, sans-serif` |
| `--odc-font-head` | Heading font stack | `system-ui, sans-serif` |
| `--odc-accent` | Primary action colour | `#1a5a61` |
| `--odc-accent-ink` | Text on accent backgrounds | `#ffffff` |
| `--odc-accent-soft` | Subtle accent fill (chips, skeletons) | `#eef6f6` |
| `--odc-price` | Price number colour | `#0e1618` |
| `--odc-ink` | Primary text colour | `#15201f` |
| `--odc-muted` | Secondary / muted text | `#5f6b68` |
| `--odc-bg` | Element background | `#ffffff` |
| `--odc-surface` | Card / panel surface | `#ffffff` |
| `--odc-border` | Border / divider colour | `rgba(20,30,28,0.12)` |
| `--odc-radius` | Corner radius (cards, panels) | `14px` |
| `--odc-radius-sm` | Smaller corner radius (buttons, chips) | `9px` |
| `--odc-shadow` | Resting shadow | `0 1px 2px rgba(16,24,22,0.06)` |
| `--odc-shadow-hover` | Hover / lifted shadow | `0 12px 30px rgba(16,24,22,0.14)` |
| `--odc-tag-bg` | Category badge background | `#15201f` |
| `--odc-tag-ink` | Category badge text | `#ffffff` |

---

## Deprecation policy

- A property, attribute, event, or CSS part may be **deprecated** with a `console.warn`
  in a minor release. It will remain functional for the rest of the major version.
- **Removal** of any item above requires a new major version (`v2`, `v3`, …).
- New major versions are co-hosted at `/components/v{n}/odisea-components.umd.js`.
  Agencies pin their embed with `data-version="{n}"` on the loader script tag.
- The loader defaults to `v1` when `data-version` is absent, emitting a console warning.
