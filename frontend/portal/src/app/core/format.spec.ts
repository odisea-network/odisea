import { describe, expect, it } from 'vitest';
import { formatDate, formatMoney } from './format';

describe('formatMoney', () => {
  it('always shows exactly two decimals', () => {
    expect(formatMoney(831.1)).toContain('831,10');
    expect(formatMoney(1000)).toContain('1000,00');
  });

  it('keeps the sign for credit notes', () => {
    expect(formatMoney(-831.1)).toContain('-');
    expect(formatMoney(-831.1)).toContain('831,10');
  });

  it('never invents precision — displays what the API sent', () => {
    // 0.1 + 0.2 style float traps must not appear: we only format single values.
    expect(formatMoney(118.5)).toContain('118,50');
  });

  it('renders em dash for missing values', () => {
    expect(formatMoney(null)).toBe('—');
    expect(formatMoney(undefined)).toBe('—');
  });
});

describe('formatDate', () => {
  it('formats ISO dates as dd.MM.yyyy', () => {
    expect(formatDate('2027-03-30')).toBe('30.03.2027 г.');
  });

  it('handles missing values', () => {
    expect(formatDate(null)).toBe('—');
  });
});
