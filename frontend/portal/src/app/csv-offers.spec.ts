import { describe, expect, it } from 'vitest';
import { parseOffersCsv } from './csv-offers';

describe('parseOffersCsv', () => {
  it('returns [] when there is no data row', () => {
    expect(parseOffersCsv('')).toEqual([]);
    expect(parseOffersCsv('title,country')).toEqual([]); // header only
  });

  it('maps columns by header name regardless of order', () => {
    const csv = 'country,title,price,board\nGR,Crete,540,HalfBoard';
    const [row] = parseOffersCsv(csv);

    expect(row.title).toBe('Crete');
    expect(row.country).toBe('GR');
    expect(row.price).toBe(540);
    expect(row.boardBasis).toBe('HalfBoard');
  });

  it('applies defaults for missing optional columns', () => {
    const [row] = parseOffersCsv('title,country\nAntalya,TR');

    expect(row.currency).toBe('EUR');
    expect(row.price).toBe(0);
    expect(row.durationNights).toBe(0);
    expect(row.tags).toEqual([]);
    expect(row.imageUrl).toBeNull();
  });

  it('splits semicolon-separated tags', () => {
    const [row] = parseOffersCsv('title,tags\nX,family;beach; luxury ');
    expect(row.tags).toEqual(['family', 'beach', 'luxury']);
  });

  it('handles quoted fields containing commas', () => {
    const csv = 'title,description\n"Trip","All-in, 7 nights, sea view"';
    const [row] = parseOffersCsv(csv);

    expect(row.title).toBe('Trip');
    expect(row.description).toBe('All-in, 7 nights, sea view');
  });

  it('parses multiple rows and ignores blank lines', () => {
    const csv = 'title,country\nA,GR\n\nB,TR\n';
    const rows = parseOffersCsv(csv);

    expect(rows.map((r) => r.title)).toEqual(['A', 'B']);
  });

  it('matches headers case-insensitively', () => {
    const [row] = parseOffersCsv('Title,COUNTRY\nA,GR');
    expect(row.title).toBe('A');
    expect(row.country).toBe('GR');
  });
});
