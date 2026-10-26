/** Month grid for the departures calendar: Monday-first, leading blanks, one cell per day. */
export interface MonthCell {
  /** yyyy-mm-dd, or null for a leading blank. */
  date: string | null;
  day: number | null;
  mark: 'ok' | 'warn' | null;
}

export interface MonthGrid {
  year: number;
  month: number;
  label: string;
  cells: MonthCell[];
}

const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

function iso(year: number, month: number, day: number): string {
  return `${year}-${String(month + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
}

/** `marks` maps yyyy-mm-dd to a mark; `month` is 0-based like Date. */
export function monthGrid(year: number, month: number, marks: Map<string, 'ok' | 'warn'>): MonthGrid {
  const first = new Date(Date.UTC(year, month, 1));
  const lead = (first.getUTCDay() + 6) % 7; // Monday = 0
  const days = new Date(Date.UTC(year, month + 1, 0)).getUTCDate();
  const cells: MonthCell[] = [];
  for (let i = 0; i < lead; i++) cells.push({ date: null, day: null, mark: null });
  for (let day = 1; day <= days; day++) {
    const date = iso(year, month, day);
    cells.push({ date, day, mark: marks.get(date) ?? null });
  }
  return { year, month, label: `${MONTHS[month]} ${year}`, cells };
}

export function shiftMonth(year: number, month: number, by: number): { year: number; month: number } {
  const d = new Date(Date.UTC(year, month + by, 1));
  return { year: d.getUTCFullYear(), month: d.getUTCMonth() };
}

/** Whole nights between two calendar dates. */
export function nightsBetween(start: string, end: string): number {
  const a = Date.parse(`${start}T00:00:00Z`);
  const b = Date.parse(`${end}T00:00:00Z`);
  if (Number.isNaN(a) || Number.isNaN(b)) return 0;
  return Math.round((b - a) / 86_400_000);
}
