import { CreateOfferRequest } from './api.service';

/**
 * Parses a small CSV paste into CreateOfferRequest rows for the operator bulk
 * import. The first non-empty line is the header; columns are matched by name
 * (case-insensitive), so column order doesn't matter and extras are ignored.
 *
 * Recognised columns: title, country, city, price, nights, board, transport,
 * currency, description, image (or imageurl), tags (semicolon-separated).
 *
 * Parsing is forgiving — missing optional columns fall back to defaults. Row-level
 * validation (board/transport enums, required fields) is the backend's job; this
 * just shapes the request, so a typo'd board still posts and comes back as a
 * per-row error rather than silently dropping.
 */
export function parseOffersCsv(text: string): CreateOfferRequest[] {
  const lines = text
    .split(/\r?\n/)
    .map((l) => l.trim())
    .filter((l) => l.length > 0);

  if (lines.length < 2) return [];

  const header = splitRow(lines[0]).map((h) => h.trim().toLowerCase());
  const col = (row: string[], name: string): string => {
    const i = header.indexOf(name);
    return i >= 0 && i < row.length ? row[i].trim() : '';
  };

  return lines.slice(1).map((line) => {
    const r = splitRow(line);
    const tags = col(r, 'tags');
    return {
      title: col(r, 'title'),
      description: col(r, 'description'),
      country: col(r, 'country'),
      city: col(r, 'city'),
      price: Number(col(r, 'price')) || 0,
      currency: col(r, 'currency') || 'EUR',
      boardBasis: col(r, 'board'),
      transport: col(r, 'transport'),
      durationNights: Number(col(r, 'nights')) || 0,
      tags: tags ? tags.split(';').map((t) => t.trim()).filter(Boolean) : [],
      imageUrl: col(r, 'image') || col(r, 'imageurl') || null,
    };
  });
}

// Minimal CSV field split: handles double-quoted fields containing commas.
function splitRow(line: string): string[] {
  const out: string[] = [];
  let cur = '';
  let inQuotes = false;
  for (let i = 0; i < line.length; i++) {
    const ch = line[i];
    if (inQuotes) {
      if (ch === '"' && line[i + 1] === '"') { cur += '"'; i++; }
      else if (ch === '"') inQuotes = false;
      else cur += ch;
    } else if (ch === '"') {
      inQuotes = true;
    } else if (ch === ',') {
      out.push(cur);
      cur = '';
    } else {
      cur += ch;
    }
  }
  out.push(cur);
  return out;
}
