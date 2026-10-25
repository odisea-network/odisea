/**
 * Runtime brand theming. An operator supplies two colours (primary, accent); the
 * portal derives the "inks" used for text, links and active navigation so they
 * reach WCAG AA on white, and picks the text colour that sits on the primary.
 * Pure functions, no DOM: BrandingService writes the result into CSS variables.
 */

export interface BrandTheme {
  primary: string;
  primaryInk: string;
  onPrimary: string;
  accent: string;
  accentInk: string;
}

const WHITE = '#ffffff';
const NEAR_BLACK = '#0b0d12';

/** Contrast an ink must reach on white — a notch above AA so hover/active shades stay readable. */
const INK_MIN_CONTRAST = 4.8;
const AA_CONTRAST = 4.5;

export type Rgb = [number, number, number];

export function parseHex(hex: string): Rgb | null {
  const value = hex.trim().replace(/^#/, '');
  const full = value.length === 3 ? value.replace(/./g, c => c + c) : value;
  if (!/^[0-9a-fA-F]{6}$/.test(full)) return null;
  return [0, 2, 4].map(i => parseInt(full.slice(i, i + 2), 16)) as Rgb;
}

export function toHex([r, g, b]: Rgb): string {
  return '#' + [r, g, b].map(v => Math.round(v).toString(16).padStart(2, '0')).join('');
}

function luminance(rgb: Rgb): number {
  const [r, g, b] = rgb.map(v => {
    const c = v / 255;
    return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
  });
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

/** WCAG contrast ratio between two colours (1 … 21). Unparseable input counts as black. */
export function contrastRatio(a: string, b: string): number {
  const la = luminance(parseHex(a) ?? [0, 0, 0]);
  const lb = luminance(parseHex(b) ?? [0, 0, 0]);
  return (Math.max(la, lb) + 0.05) / (Math.min(la, lb) + 0.05);
}

/** Linear blend of `hex` towards `target` by `amount` (0 … 1). */
export function mix(hex: string, target: string, amount: number): string {
  const from = parseHex(hex) ?? [0, 0, 0];
  const to = parseHex(target) ?? [0, 0, 0];
  return toHex(from.map((v, i) => v + (to[i] - v) * amount) as Rgb);
}

/** Darkens a brand colour in 10% steps until it is legible as text on white. */
export function inkFor(hex: string): string {
  let ink = hex;
  for (let step = 0; step < 14 && contrastRatio(ink, WHITE) < INK_MIN_CONTRAST; step++) {
    ink = mix(ink, '#000000', 0.1);
  }
  return ink;
}

/** White text on the primary unless the primary is so light that near-black reads better. */
export function onColour(hex: string): string {
  const onWhite = contrastRatio(hex, WHITE);
  const onDark = contrastRatio(hex, NEAR_BLACK);
  return onWhite >= AA_CONTRAST || onWhite >= onDark ? WHITE : NEAR_BLACK;
}

export const DEFAULT_PRIMARY = '#1946b8';
export const DEFAULT_ACCENT = '#e8a23a';

export function deriveBrandTheme(primary: string, accent: string): BrandTheme {
  const p = parseHex(primary) ? toHex(parseHex(primary)!) : DEFAULT_PRIMARY;
  const a = parseHex(accent) ? toHex(parseHex(accent)!) : DEFAULT_ACCENT;
  return {
    primary: p,
    primaryInk: inkFor(p),
    onPrimary: onColour(p),
    accent: a,
    accentInk: inkFor(a),
  };
}
