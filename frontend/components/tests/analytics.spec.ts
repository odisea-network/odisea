/**
 * Analytics instrumentation tests for the embed components.
 *
 * Runs against the *built* UMD bundle (same bootstrap as contract.spec.ts).
 * Build the bundle (`npm run build`) before running the suite.
 */

import { readFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { beforeAll, beforeEach, afterEach, describe, it, expect, vi } from 'vitest';

const __dirname = dirname(fileURLToPath(import.meta.url));
const BUNDLE = resolve(__dirname, '../dist/v1/odisea-components.umd.js');

const OFFER = {
  id: 'o1',
  title: 'Summer in Santorini',
  country: 'Greece',
  price: 899,
  nights: 7,
  imageUrl: '',
};

// ── bundle bootstrap ───────────────────────────────────────────────────────────

beforeAll(() => {
  const src = readFileSync(BUNDLE, 'utf-8');
  // eslint-disable-next-line no-new-func
  new Function(src)();
});

// A synchronous IntersectionObserver stub: observing an element immediately
// reports it as intersecting, so the impression fires within connectedCallback.
class SyncIntersectionObserver {
  private _cb: (entries: { isIntersecting: boolean; target: Element }[]) => void;
  constructor(cb: (entries: { isIntersecting: boolean; target: Element }[]) => void) {
    this._cb = cb;
  }
  observe(target: Element) {
    this._cb([{ isIntersecting: true, target }]);
  }
  unobserve() {}
  disconnect() {}
}

let beaconCalls: { url: string; body: string }[] = [];

beforeEach(() => {
  beaconCalls = [];
  (globalThis as any).IntersectionObserver = SyncIntersectionObserver;
  (globalThis as any).navigator ??= {};
  (globalThis.navigator as any).sendBeacon = vi.fn((url: string, blob: Blob) => {
    // happy-dom Blob exposes text() asynchronously; capture eagerly via a stored value
    beaconCalls.push({ url, body: (blob as any)._buffer?.toString?.() ?? '' });
    return true;
  });
});

const mounted: Element[] = [];
function mount<T extends Element>(el: T): T {
  document.body.appendChild(el);
  mounted.push(el);
  return el;
}
afterEach(() => {
  mounted.forEach(el => el.parentNode?.removeChild(el));
  mounted.length = 0;
  vi.restoreAllMocks();
});

// ── tests ───────────────────────────────────────────────────────────────────────

describe('od-offer-card impression', () => {
  it('dispatches od-impression when scrolled into view', () => {
    const el = document.createElement('od-offer-card') as any;
    el.offer = { ...OFFER };
    el.publicationKey = 'pub-key-1';

    let detail: any = null;
    el.addEventListener('od-impression', (e: CustomEvent) => { detail = e.detail; });

    // Appending connects the element -> IntersectionObserver.observe fires sync.
    mount(el);

    expect(detail).not.toBeNull();
    expect(detail.publicationKey).toBe('pub-key-1');
    expect(detail.offerId).toBe('o1');
  });

  it('sends a beacon with eventType Impression when a publication-key is set', () => {
    const el = document.createElement('od-offer-card') as any;
    el.offer = { ...OFFER };
    el.publicationKey = 'pub-key-2';
    mount(el);

    expect(globalThis.navigator.sendBeacon).toHaveBeenCalledTimes(1);
    const [url] = (globalThis.navigator.sendBeacon as any).mock.calls[0];
    expect(url).toBe('/api/v1/events');
  });

  it('does not send a beacon when publication-key is absent', () => {
    const el = document.createElement('od-offer-card') as any;
    el.offer = { ...OFFER };
    mount(el);

    // DOM event still fires, but no network beacon without attribution.
    expect(globalThis.navigator.sendBeacon).not.toHaveBeenCalled();
  });
});
