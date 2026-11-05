import { describe, expect, it } from 'vitest';
import { monthGrid, nightsBetween, shiftMonth } from './calendar';

describe('monthGrid', () => {
  it('starts on Monday with leading blanks and marks the given dates', () => {
    // 1 April 2027 is a Thursday → three blanks (Mon, Tue, Wed).
    const grid = monthGrid(2027, 3, new Map([['2027-04-03', 'ok'], ['2027-04-17', 'warn']]));
    expect(grid.label).toBe('April 2027');
    expect(grid.cells.slice(0, 3).every(c => c.date === null)).toBe(true);
    expect(grid.cells.length).toBe(3 + 30);
    expect(grid.cells.find(c => c.date === '2027-04-03')?.mark).toBe('ok');
    expect(grid.cells.find(c => c.date === '2027-04-17')?.mark).toBe('warn');
    expect(grid.cells.find(c => c.date === '2027-04-18')?.mark).toBeNull();
  });

  it('shifts across year boundaries', () => {
    expect(shiftMonth(2026, 11, 1)).toEqual({ year: 2027, month: 0 });
    expect(shiftMonth(2027, 0, -1)).toEqual({ year: 2026, month: 11 });
  });
});

describe('nightsBetween', () => {
  it('counts whole nights, immune to DST', () => {
    expect(nightsBetween('2027-03-27', '2027-04-03')).toBe(7);
    expect(nightsBetween('2027-04-03', '2027-04-03')).toBe(0);
    expect(nightsBetween('bad', '2027-04-03')).toBe(0);
  });
});
