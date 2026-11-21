import { t } from './i18n/i18n';

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

/** Instants (status changes, payment deadlines): date and time in the viewer's zone. */
export function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return '—';
  const date = new Date(iso);
  return Number.isNaN(date.getTime())
    ? iso
    : new Intl.DateTimeFormat('bg-BG', {
        day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit',
      }).format(date);
}

export type Urgency = 'over' | 'soon' | 'ok';

export interface RelativeDeadline {
  /** Human phrase: "Просрочено с 3 ч", "след 19 ч", "след 2 дни 17 ч", "след 4 дни". */
  label: string;
  urgency: Urgency;
}

const HOUR = 60 * 60 * 1000;
const DAY = 24 * HOUR;

/**
 * Deadlines are shown twice: the absolute instant and how far away it is. Under a
 * day is "soon"; past is "over". Returns null when there is no deadline.
 */
export function relativeDeadline(iso: string | null | undefined, now = new Date()): RelativeDeadline | null {
  if (!iso) return null;
  const at = Date.parse(iso);
  if (Number.isNaN(at)) return null;

  const diff = at - now.getTime();
  const span = (ms: number): string => {
    const days = Math.floor(ms / DAY);
    const hours = Math.floor((ms % DAY) / HOUR);
    const minutes = Math.floor((ms % HOUR) / 60_000);
    const dayText = t(days === 1 ? 'deadline.day' : 'deadline.days', { n: days });
    if (days >= 3) return dayText;
    if (days >= 1) return hours > 0 ? `${dayText} ${t('deadline.hours', { n: hours })}` : dayText;
    if (hours >= 1) return t('deadline.hours', { n: hours });
    return t('deadline.minutes', { n: Math.max(minutes, 1) });
  };

  if (diff < 0) return { label: t('deadline.overdue', { span: span(-diff) }), urgency: 'over' };
  return { label: t('deadline.in', { span: span(diff) }), urgency: diff < DAY ? 'soon' : 'ok' };
}
