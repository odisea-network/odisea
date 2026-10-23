/**
 * Display helpers. Money values arrive fully calculated from the API;
 * these only FORMAT — rounding or arithmetic here would be a bug.
 */
export function formatMoney(amount: number | null | undefined, currency = 'EUR'): string {
  if (amount === null || amount === undefined) return '—';
  return new Intl.NumberFormat('bg-BG', {
    style: 'currency',
    currency,
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(amount);
}

const DATE_ONLY = /^(\d{4})-(\d{2})-(\d{2})$/;

export function formatDate(iso: string | null | undefined): string {
  if (!iso) return '—';
  // Date-only values (check-in/out, issue dates) are calendar dates, not
  // instants: formatting them via `new Date()` would shift them a day for
  // users west of UTC. Format them literally.
  const dateOnly = DATE_ONLY.exec(iso);
  if (dateOnly) return `${dateOnly[3]}.${dateOnly[2]}.${dateOnly[1]} г.`;

  const date = new Date(iso);
  return Number.isNaN(date.getTime())
    ? iso
    : new Intl.DateTimeFormat('bg-BG', { day: '2-digit', month: '2-digit', year: 'numeric' }).format(date);
}
