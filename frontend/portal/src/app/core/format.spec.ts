import { describe, expect, it } from 'vitest';
import { formatDate, formatMoney, relativeDeadline } from './format';

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
  it('formats date-only values literally — immune to the viewer timezone', () => {
    // Regression: new Date('2027-03-30') is UTC midnight and rendered a day
    // early for anyone west of UTC. Calendar dates must never pass через Date.
    expect(formatDate('2027-03-30')).toBe('30.03.2027 г.');
  });

  it('handles missing values', () => {
    expect(formatDate(null)).toBe('—');
  });
});

describe('relativeDeadline', () => {
  const now = new Date('2026-10-25T01:00:00Z');
  const at = (hours: number) => new Date(now.getTime() + hours * 3_600_000).toISOString();

  it('flags past deadlines as overdue', () => {
    expect(relativeDeadline(at(-3), now)).toEqual({ label: 'Overdue 3h', urgency: 'over' });
    expect(relativeDeadline(at(-50), now)).toEqual({ label: 'Overdue 2 days 2h', urgency: 'over' });
  });

  it('treats anything under a day as soon', () => {
    expect(relativeDeadline(at(19), now)).toEqual({ label: 'in 19h', urgency: 'soon' });
    expect(relativeDeadline(at(0.5), now)).toEqual({ label: 'in 30 min', urgency: 'soon' });
  });

  it('counts days beyond that, dropping hours after three days', () => {
    expect(relativeDeadline(at(65), now)).toEqual({ label: 'in 2 days 17h', urgency: 'ok' });
    expect(relativeDeadline(at(24 * 4), now)).toEqual({ label: 'in 4 days', urgency: 'ok' });
  });

  it('has nothing to say without a deadline', () => {
    expect(relativeDeadline(null, now)).toBeNull();
    expect(relativeDeadline('garbage', now)).toBeNull();
  });
});
