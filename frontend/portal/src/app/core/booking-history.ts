import { TimelineItem } from '../shared/ui/timeline/timeline';
import { bookingStatusMeta } from '../shared/ui/status-chip/status-chip';
import { formatDateTime } from './format';

export interface HistoryEntry {
  at: string;
  from: string;
  to: string;
  note: string | null;
  /** Who did it, as the API phrases it for the viewer (agency sees roles, operators see types). */
  actor: string;
}

const TONE: Record<string, TimelineItem['tone']> = {
  Confirmed: 'ok',
  AwaitingPayment: 'warn',
  PriceConfirmed: 'info',
  Rejected: 'bad',
  Failed: 'bad',
};

const NO_PREVIOUS = new Set(['', '-', '—']);

function hasPrevious(entry: HistoryEntry): boolean {
  return !NO_PREVIOUS.has(entry.from.trim()) && entry.from !== entry.to;
}

/** Status history → timeline, newest first. */
export function historyTimeline(history: HistoryEntry[]): TimelineItem[] {
  return [...history]
    .sort((a, b) => Date.parse(b.at) - Date.parse(a.at))
    .map(entry => {
      const meta = bookingStatusMeta(entry.to);
      return {
        title: meta.label,
        detail: hasPrevious(entry) ? `from ${bookingStatusMeta(entry.from).label.toLowerCase()}` : null,
        meta: `${entry.actor} · ${formatDateTime(entry.at)}`,
        note: entry.note,
        icon: meta.icon,
        tone: TONE[entry.to] ?? 'neutral',
      };
    });
}
