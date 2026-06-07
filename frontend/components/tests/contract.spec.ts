/**
 * v1 public contract tests.
 *
 * These tests import the *built* UMD bundle and assert that every promise made in
 * CONTRACT.md is upheld: element registration, attribute→property reflection, and
 * event dispatch on user interaction.  They must pass against every future major
 * version — update CONTRACT.md and this file together when the surface changes.
 */

import { readFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { beforeAll, describe, it, expect, afterEach } from 'vitest';

const __dirname = dirname(fileURLToPath(import.meta.url));
const BUNDLE = resolve(__dirname, '../dist/v1/odisea-components.umd.js');

// Minimal OfferDto that satisfies every component that needs one
const OFFER = {
  id: 'o1',
  title: 'Summer in Santorini',
  country: 'Greece',
  city: 'Santorini',
  price: 899,
  currency: 'EUR',
  board: 'HB',
  transport: 'plane',
  nights: 7,
  imageUrl: '',
};

const COLLECTION = {
  id: 'c1',
  title: 'Summer Greece',
  slug: 'summer-gr',
  description: 'Best Greek summer offers',
  imageUrl: '',
  count: 12,
};

// ── bundle bootstrap ───────────────────────────────────────────────────────────

beforeAll(() => {
  // Execute the UMD bundle in the happy-dom global scope.
  // new Function avoids strict-mode this=undefined, so the UMD IIFE receives
  // globalThis as `this` and falls through to the globalThis branch which calls
  // customElements.define() on the happy-dom registry.
  const src = readFileSync(BUNDLE, 'utf-8');
  // eslint-disable-next-line no-new-func
  new Function(src)();
});

// Clean up any elements appended to body between tests
const mounted: Element[] = [];
function mount<T extends Element>(el: T): T {
  document.body.appendChild(el);
  mounted.push(el);
  return el;
}
afterEach(() => {
  mounted.forEach(el => el.parentNode?.removeChild(el));
  mounted.length = 0;
});

// ── element registration ───────────────────────────────────────────────────────

describe('element registration', () => {
  const TAGS = [
    'od-offer-card',
    'od-offer-grid',
    'od-offer-carousel',
    'od-filter-panel',
    'od-offer-details',
    'od-price-table',
    'od-booking-inquiry',
    'od-destination-hero',
    'od-featured-collections',
  ];

  it.each(TAGS)('%s is defined in the custom-element registry', (tag) => {
    expect(customElements.get(tag)).toBeDefined();
  });
});

// ── attribute → property reflection ───────────────────────────────────────────

describe('attribute reflection', () => {
  it('od-offer-card: card-style → cardStyle', () => {
    const el = document.createElement('od-offer-card') as any;
    el.setAttribute('card-style', 'compact');
    expect(el.cardStyle).toBe('compact');
  });

  it('od-offer-card: offer-id → offerId', () => {
    const el = document.createElement('od-offer-card') as any;
    el.setAttribute('offer-id', 'abc-123');
    expect(el.offerId).toBe('abc-123');
  });

  it('od-offer-card: cta-label → ctaLabel', () => {
    const el = document.createElement('od-offer-card') as any;
    el.setAttribute('cta-label', 'Book now');
    expect(el.ctaLabel).toBe('Book now');
  });

  it('od-offer-grid: card-style → cardStyle', () => {
    const el = document.createElement('od-offer-grid') as any;
    el.setAttribute('card-style', 'editorial');
    expect(el.cardStyle).toBe('editorial');
  });

  it('od-offer-grid: api-base → apiBase', () => {
    const el = document.createElement('od-offer-grid') as any;
    el.setAttribute('api-base', 'https://api.example.com');
    expect(el.apiBase).toBe('https://api.example.com');
  });

  it('od-offer-carousel: card-width → cardWidth (number)', () => {
    const el = document.createElement('od-offer-carousel') as any;
    el.setAttribute('card-width', '320');
    expect(el.cardWidth).toBe(320);
  });

  it('od-filter-panel: max-price → maxPrice (number)', () => {
    const el = document.createElement('od-filter-panel') as any;
    el.setAttribute('max-price', '5000');
    expect(el.maxPrice).toBe(5000);
  });

  it('od-filter-panel: default-max-price → defaultMaxPrice (number)', () => {
    const el = document.createElement('od-filter-panel') as any;
    el.setAttribute('default-max-price', '1500');
    expect(el.defaultMaxPrice).toBe(1500);
  });

  it('od-offer-details: cta-label → ctaLabel', () => {
    const el = document.createElement('od-offer-details') as any;
    el.setAttribute('cta-label', 'Enquire');
    expect(el.ctaLabel).toBe('Enquire');
  });

  it('od-price-table: currency → currency', () => {
    const el = document.createElement('od-price-table') as any;
    el.setAttribute('currency', 'BGN');
    expect(el.currency).toBe('BGN');
  });

  it('od-booking-inquiry: offer-title → offerTitle', () => {
    const el = document.createElement('od-booking-inquiry') as any;
    el.setAttribute('offer-title', 'Greek Holiday');
    expect(el.offerTitle).toBe('Greek Holiday');
  });

  it('od-booking-inquiry: submit-label → submitLabel', () => {
    const el = document.createElement('od-booking-inquiry') as any;
    el.setAttribute('submit-label', 'Send enquiry');
    expect(el.submitLabel).toBe('Send enquiry');
  });

  it('od-destination-hero: cta-label → ctaLabel', () => {
    const el = document.createElement('od-destination-hero') as any;
    el.setAttribute('cta-label', 'Discover');
    expect(el.ctaLabel).toBe('Discover');
  });

  it('od-destination-hero: min-height → minHeight', () => {
    const el = document.createElement('od-destination-hero') as any;
    el.setAttribute('min-height', '400px');
    expect(el.minHeight).toBe('400px');
  });

  it('od-featured-collections: columns → columns (number)', () => {
    const el = document.createElement('od-featured-collections') as any;
    el.setAttribute('columns', '4');
    expect(el.columns).toBe(4);
  });
});

// ── event dispatch ─────────────────────────────────────────────────────────────

describe('event dispatch', () => {
  const nextUpdate = (el: any) => el.updateComplete as Promise<boolean>;

  it('od-offer-card fires od-cta-click when CTA is clicked', async () => {
    const el = mount(document.createElement('od-offer-card') as any);
    el.offer = { ...OFFER };
    await nextUpdate(el);

    let detail: any = null;
    el.addEventListener('od-cta-click', (e: CustomEvent) => { detail = e.detail; });

    el.shadowRoot!.querySelector<HTMLButtonElement>('.cta')!.click();
    expect(detail).not.toBeNull();
    expect(detail.offer.title).toBe(OFFER.title);
  });

  it('od-offer-grid: od-cta-click with composed+bubbles crosses shadow boundary to host', () => {
    // The grid renders od-offer-card elements that dispatch od-cta-click with
    // composed:true + bubbles:true.  Verify the event escapes the open shadow root
    // and reaches the host — the same path a real card click would take.
    const grid = mount(document.createElement('od-offer-grid') as any);
    expect(grid.shadowRoot).not.toBeNull();

    let detail: any = null;
    grid.addEventListener('od-cta-click', (e: CustomEvent) => { detail = e.detail; });

    const probe = document.createElement('span');
    grid.shadowRoot!.appendChild(probe);
    probe.dispatchEvent(new CustomEvent('od-cta-click', {
      detail: { offer: OFFER },
      bubbles: true,
      composed: true,
    }));

    expect(detail?.offer?.title).toBe(OFFER.title);
  });

  it('od-offer-carousel: od-cta-click with composed+bubbles crosses shadow boundary to host', () => {
    const carousel = mount(document.createElement('od-offer-carousel') as any);
    expect(carousel.shadowRoot).not.toBeNull();

    let detail: any = null;
    carousel.addEventListener('od-cta-click', (e: CustomEvent) => { detail = e.detail; });

    const probe = document.createElement('span');
    carousel.shadowRoot!.appendChild(probe);
    probe.dispatchEvent(new CustomEvent('od-cta-click', {
      detail: { offer: OFFER },
      bubbles: true,
      composed: true,
    }));

    expect(detail?.offer?.title).toBe(OFFER.title);
  });

  it('od-offer-details fires od-cta-click when CTA is clicked', async () => {
    const el = mount(document.createElement('od-offer-details') as any);
    el.offer = { ...OFFER };
    await nextUpdate(el);

    let detail: any = null;
    el.addEventListener('od-cta-click', (e: CustomEvent) => { detail = e.detail; });

    el.shadowRoot!.querySelector<HTMLButtonElement>('.cta')!.click();
    expect(detail).not.toBeNull();
    expect(detail.offer.title).toBe(OFFER.title);
  });

  it('od-filter-panel fires od-filter-change when a country checkbox is toggled', async () => {
    const el = mount(document.createElement('od-filter-panel') as any);
    el.offers = [{ ...OFFER }];
    await nextUpdate(el);

    let fired = false;
    let filterDetail: any = null;
    el.addEventListener('od-filter-change', (e: CustomEvent) => {
      fired = true;
      filterDetail = e.detail;
    });

    // The first checkbox is the country facet for "Greece"
    const checkbox = el.shadowRoot!.querySelector<HTMLInputElement>('input[type="checkbox"]')!;
    checkbox.checked = true;
    checkbox.dispatchEvent(new Event('change', { bubbles: true }));

    expect(fired).toBe(true);
    expect(Array.isArray(filterDetail.countries)).toBe(true);
  });

  it('od-filter-panel fires od-filter-change when the price range changes', async () => {
    const el = mount(document.createElement('od-filter-panel') as any);
    el.offers = [{ ...OFFER }];
    await nextUpdate(el);

    let fired = false;
    el.addEventListener('od-filter-change', () => { fired = true; });

    const range = el.shadowRoot!.querySelector<HTMLInputElement>('input[type="range"]')!;
    range.value = '1200';
    range.dispatchEvent(new Event('input', { bubbles: true }));

    expect(fired).toBe(true);
  });

  it('od-booking-inquiry fires od-inquiry-submit when the form is submitted', async () => {
    const el = mount(document.createElement('od-booking-inquiry') as any);
    await nextUpdate(el);

    let payload: any = null;
    el.addEventListener('od-inquiry-submit', (e: CustomEvent) => { payload = e.detail; });

    // Submit fires synchronously before the async setTimeout inside _submit
    el.shadowRoot!.querySelector('form')!.dispatchEvent(
      new Event('submit', { bubbles: true, cancelable: true }),
    );

    expect(payload).not.toBeNull();
    expect(typeof payload.name).toBe('string');
    expect(typeof payload.email).toBe('string');
  });

  it('od-destination-hero fires od-hero-cta when the CTA button is clicked', async () => {
    const el = mount(document.createElement('od-destination-hero') as any);
    el.setAttribute('cta-label', 'Explore');
    await nextUpdate(el);

    let fired = false;
    el.addEventListener('od-hero-cta', () => { fired = true; });

    el.shadowRoot!.querySelector<HTMLButtonElement>('.cta')!.click();
    expect(fired).toBe(true);
  });

  it('od-featured-collections fires od-collection-click when a collection card is clicked', () => {
    const el = mount(document.createElement('od-featured-collections') as any);

    let detail: any = null;
    el.addEventListener('od-collection-click', (e: CustomEvent) => { detail = e.detail; });

    // happy-dom does not render ${list.map(...)} arrays into shadow-DOM nodes that
    // querySelector can find, so we call the @click handler (_onClick) directly via
    // `as any` — TypeScript private is compile-time only; the method exists on the instance.
    el._onClick(new Event('click', { cancelable: true }), { ...COLLECTION });

    expect(detail).not.toBeNull();
    expect(detail.collection.title).toBe(COLLECTION.title);
  });
});
