import { afterEach, describe, expect, it, vi } from 'vitest';
import { hasOfferSource, resolveOffersUrl } from '../src/od-fetch.js';

describe('resolveOffersUrl', () => {
  afterEach(() => vi.restoreAllMocks());

  it('uses endpoint verbatim when set', async () => {
    const url = await resolveOffersUrl('https://api.example', { endpoint: 'https://x/y' });
    expect(url).toBe('https://x/y');
  });

  it('resolves a publication key through the manifest', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue({
      ok: true,
      json: async () => ({ offersUrl: '/api/v1/collections/abc-123/offers' }),
    } as Response);

    const url = await resolveOffersUrl('https://api.example', { publication: 'blue-gr-summer' });

    expect(fetchMock).toHaveBeenCalledWith('https://api.example/api/v1/publications/blue-gr-summer');
    expect(url).toBe('https://api.example/api/v1/collections/abc-123/offers');
  });

  it('throws when the manifest fetch fails', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue({ ok: false, status: 404 } as Response);
    await expect(resolveOffersUrl('https://api.example', { publication: 'missing' }))
      .rejects.toThrow('manifest HTTP 404');
  });

  it('falls back to the slug path for a collection (deprecated)', async () => {
    const url = await resolveOffersUrl('https://api.example', { collection: 'summer-greece' });
    expect(url).toBe('https://api.example/api/v1/collections/summer-greece/offers');
  });

  it('prefers endpoint > publication > collection', async () => {
    const url = await resolveOffersUrl('', { endpoint: 'E', publication: 'P', collection: 'C' });
    expect(url).toBe('E');
  });

  it('returns empty string when no source is set', async () => {
    expect(await resolveOffersUrl('https://api.example', {})).toBe('');
  });
});

describe('hasOfferSource', () => {
  it('is true when any source is set, false otherwise', () => {
    expect(hasOfferSource({ publication: 'k' })).toBe(true);
    expect(hasOfferSource({ collection: 's' })).toBe(true);
    expect(hasOfferSource({ endpoint: 'u' })).toBe(true);
    expect(hasOfferSource({})).toBe(false);
  });
});
