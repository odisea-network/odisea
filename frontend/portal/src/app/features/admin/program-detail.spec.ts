import { describe, expect, it } from 'vitest';
import { publishChecks } from './program-detail';

const program = {
  name: 'Antalya Spring 2027',
  destinationId: 'd1',
  departures: [
    { id: 'a', startDate: '2027-04-03', endDate: '2027-04-10', nights: 7, transportNote: 'FB 1471' },
    { id: 'b', startDate: '2027-04-17', endDate: '2027-04-24', nights: 7, transportNote: null },
  ],
  hotelIds: ['h1', 'h2'],
};

describe('publishChecks', () => {
  it('passes everything for a complete program', () => {
    const checks = publishChecks({ ...program, departures: [program.departures[0]] }, 2, '2026-10-26');
    expect(checks.every(c => c.ok)).toBe(true);
  });

  it('flags missing transport and unmapped hotels without blocking the rest', () => {
    const checks = publishChecks(program, 1, '2026-10-26');
    expect(checks.find(c => c.title.startsWith('Transport'))?.ok).toBe(false);
    expect(checks.find(c => c.title.startsWith('Hotels mapped'))?.sub).toBe('1 of 2 mapped');
    expect(checks.find(c => c.title.startsWith('At least one upcoming'))?.ok).toBe(true);
  });

  it('counts only future departures as upcoming', () => {
    const checks = publishChecks(program, 2, '2027-05-01');
    expect(checks.find(c => c.title.startsWith('At least one upcoming'))?.ok).toBe(false);
  });

  it('reports the mapping check as pending while mappings load', () => {
    const check = publishChecks(program, null, '2026-10-26').find(c => c.title.startsWith('Hotels mapped'));
    expect(check?.ok).toBe(false);
    expect(check?.sub).toBe('Checking…');
  });
});
