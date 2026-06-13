import { describe, expect, it } from 'vitest';
import { buildConfigJson, parseConfig } from './connection-config';

describe('buildConfigJson', () => {
  it('includes url and non-blank field-map rows', () => {
    const json = buildConfigJson('https://x/feed', [
      { canonical: 'externalId', supplier: 'id' },
      { canonical: 'price', supplier: 'cost' },
    ]);
    expect(JSON.parse(json)).toEqual({
      url: 'https://x/feed',
      fieldMap: { externalId: 'id', price: 'cost' },
    });
  });

  it('drops blank url and blank rows', () => {
    const json = buildConfigJson('   ', [
      { canonical: 'title', supplier: '  ' },
      { canonical: 'country', supplier: 'dest' },
    ]);
    expect(JSON.parse(json)).toEqual({ fieldMap: { country: 'dest' } });
  });

  it('omits fieldMap entirely when no usable rows', () => {
    expect(JSON.parse(buildConfigJson('https://x', []))).toEqual({ url: 'https://x' });
  });
});

describe('parseConfig', () => {
  it('round-trips a built config', () => {
    const json = buildConfigJson('https://x', [{ canonical: 'board', supplier: 'mealPlan' }]);
    const { url, fieldMap } = parseConfig(json);
    expect(url).toBe('https://x');
    expect(fieldMap).toEqual([{ canonical: 'board', supplier: 'mealPlan' }]);
  });

  it('degrades to empty inputs on malformed or empty json', () => {
    expect(parseConfig('not json')).toEqual({ url: '', fieldMap: [] });
    expect(parseConfig('')).toEqual({ url: '', fieldMap: [] });
    expect(parseConfig('{}')).toEqual({ url: '', fieldMap: [] });
  });
});
