// Canonical offer fields a connector understands. The field-map editor maps each
// of these onto the supplier's own field/element name (see the backend connectors).
export const CANONICAL_FIELDS = [
  'externalId', 'title', 'description', 'country', 'city', 'price', 'currency',
  'board', 'transport', 'nights', 'imageUrl', 'tags', 'offer',
];

export interface FieldMapRow {
  canonical: string;
  supplier: string;
}

// Shape a connection's url + field map into the ConfigJson the connectors read.
// Empty url and empty/blank rows are dropped so the blob stays minimal.
export function buildConfigJson(url: string, fieldMap: FieldMapRow[]): string {
  const cfg: { url?: string; fieldMap?: Record<string, string> } = {};

  if (url.trim()) cfg.url = url.trim();

  const map: Record<string, string> = {};
  for (const row of fieldMap) {
    if (row.canonical && row.supplier.trim()) map[row.canonical] = row.supplier.trim();
  }
  if (Object.keys(map).length > 0) cfg.fieldMap = map;

  return JSON.stringify(cfg);
}

// Parse a stored ConfigJson back into editable url + field-map rows. Malformed
// JSON degrades to empty inputs rather than throwing.
export function parseConfig(configJson: string | null | undefined): { url: string; fieldMap: FieldMapRow[] } {
  try {
    const cfg = JSON.parse(configJson || '{}');
    const url = typeof cfg.url === 'string' ? cfg.url : '';
    const map = cfg.fieldMap && typeof cfg.fieldMap === 'object' ? cfg.fieldMap : {};
    const fieldMap = Object.entries(map)
      .filter(([, v]) => typeof v === 'string')
      .map(([canonical, supplier]) => ({ canonical, supplier: supplier as string }));
    return { url, fieldMap };
  } catch {
    return { url: '', fieldMap: [] };
  }
}
