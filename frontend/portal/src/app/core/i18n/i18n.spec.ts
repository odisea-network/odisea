import { describe, expect, it } from 'vitest';
import { BG } from './bg';
import { t, tOptional } from './i18n';

function leaves(node: unknown, path: string[] = []): [string, string][] {
  if (typeof node === 'string') return [[path.join('.'), node]];
  return Object.entries(node as Record<string, unknown>).flatMap(([key, value]) => leaves(value, [...path, key]));
}

describe('i18n', () => {
  it('returns the wording for a key', () => {
    expect(t('common.cancel')).toBe('Отказ');
  });

  it('fills placeholders and leaves unknown ones visible', () => {
    expect(t('kit.range', { from: 1, to: 20, total: 57 })).toBe('1–20 от 57');
    expect(t('kit.range', { from: 1, to: 20 })).toBe('1–20 от {total}');
  });

  it('answers null for a run-time key the dictionary does not have', () => {
    expect(tOptional('bookingStatus.Confirmed')).toBe('Потвърдена');
    expect(tOptional('bookingStatus.SomethingNew')).toBeNull();
    expect(tOptional('bookingStatus')).toBeNull();
  });

  it('has no empty wording and no leftover English placeholders', () => {
    for (const [key, text] of leaves(BG)) {
      expect(text.trim(), key).not.toBe('');
      expect(text, key).not.toMatch(/TODO|\bTBD\b/);
    }
  });
});
