import { describe, expect, it } from 'vitest';
import {
  DEFAULT_ACCENT, DEFAULT_PRIMARY, contrastRatio, deriveBrandTheme, inkFor, mix, onColour,
  parseHex, toHex,
} from './theme';

describe('colour maths', () => {
  it('parses 6- and 3-digit hex, rejects anything else', () => {
    expect(parseHex('#1946B8')).toEqual([25, 70, 184]);
    expect(parseHex('fff')).toEqual([255, 255, 255]);
    expect(parseHex('#12345')).toBeNull();
    expect(parseHex('red')).toBeNull();
    expect(toHex([25, 70, 184])).toBe('#1946b8');
  });

  it('computes WCAG contrast', () => {
    expect(contrastRatio('#000000', '#ffffff')).toBeCloseTo(21, 0);
    expect(contrastRatio('#ffffff', '#ffffff')).toBe(1);
  });

  it('mixes linearly', () => {
    expect(mix('#000000', '#ffffff', 0.5)).toBe('#808080');
    expect(mix('#1946b8', '#000000', 0)).toBe('#1946b8');
  });
});

describe('deriveBrandTheme', () => {
  it('keeps a dark primary as its own ink and puts white text on it', () => {
    const theme = deriveBrandTheme('#1946B8', '#E8A23A');
    expect(theme.primary).toBe('#1946b8');
    expect(theme.primaryInk).toBe('#1946b8');
    expect(theme.onPrimary).toBe('#ffffff');
  });

  it('darkens a light brand until it reads on white, and flips the on-colour to near-black', () => {
    const theme = deriveBrandTheme('#E8A23A', '#1946B8');
    expect(theme.primaryInk).not.toBe('#e8a23a');
    expect(contrastRatio(theme.primaryInk, '#ffffff')).toBeGreaterThanOrEqual(4.8);
    expect(theme.onPrimary).toBe('#0b0d12');
    // The accent gets the same treatment regardless of its role.
    expect(contrastRatio(theme.accentInk, '#ffffff')).toBeGreaterThanOrEqual(4.8);
  });

  it('every ink reaches AA on white for the operator palette offered in the design', () => {
    for (const colour of ['#1946B8', '#C2410C', '#0F5E57', '#5B2A86', '#E8A23A', '#0E8C83', '#D9467A', '#3B82F6', '#ffffff']) {
      expect(contrastRatio(inkFor(colour), '#ffffff')).toBeGreaterThanOrEqual(4.5);
    }
  });

  it('text on the primary is always legible', () => {
    for (const colour of ['#1946B8', '#C2410C', '#E8A23A', '#ffffff', '#000000', '#80ff00']) {
      expect(contrastRatio(colour, onColour(colour))).toBeGreaterThanOrEqual(3);
    }
  });

  it('falls back to the design defaults on invalid input', () => {
    const theme = deriveBrandTheme('not-a-colour', '');
    expect(theme.primary).toBe(DEFAULT_PRIMARY);
    expect(theme.accent).toBe(DEFAULT_ACCENT);
  });
});
