import {
  CommissionBase, CommissionType, InstallmentRequest, LedgerAuditEventDto, LedgerInstallmentDto, LedgerInstallmentLabel,
  LedgerReservationStatus,
} from '../../../core/api.types';
import { formatDateTime, formatMoney } from '../../../core/format';
import { t, tOptional } from '../../../core/i18n/i18n';
import type { IconName } from '../../../shared/ui';
import type { TimelineItem } from '../../../shared/ui/timeline/timeline';

export const CURRENCIES = ['EUR', 'USD', 'GBP', 'CHF', 'TRY'] as const;
export const STATUSES: LedgerReservationStatus[] = ['Draft', 'Confirmed', 'Completed', 'Cancelled'];
export const MAX_INSTALLMENTS = 24;

type Tone = 'neutral' | 'ok' | 'warn' | 'bad' | 'brand';

/** One editable line of a payment schedule; amount is null while the field is empty. */
export interface ScheduleRow {
  label: LedgerInstallmentLabel;
  amount: number | null;
  dueDate: string;
}

/** Today's calendar date where the user sits, as the API writes dates. */
export function isoToday(now = new Date()): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

/** Cents, half away from zero — the same rule the API applies, so a preview never disagrees with it. */
export function roundMoney(amount: number): number {
  const cents = Math.round((Math.abs(amount) + Number.EPSILON) * 100);
  return (Math.sign(amount) * cents) / 100;
}

export function scheduleTotal(rows: ScheduleRow[]): number {
  return roundMoney(rows.reduce((sum, row) => sum + (row.amount ?? 0), 0));
}

/** What the schedule leaves without a due date (positive) or plans beyond the price (negative). */
export function scheduleDifference(operatorTotal: number | null, rows: ScheduleRow[]): number {
  return roundMoney((operatorTotal ?? 0) - scheduleTotal(rows));
}

export function scheduleValid(rows: ScheduleRow[]): boolean {
  return rows.length <= MAX_INSTALLMENTS && rows.every(row => (row.amount ?? 0) > 0 && !!row.dueDate);
}

/** A deposit given as a percentage is only a way to type the amounts: the API stores amounts. */
export function splitDeposit(operatorTotal: number, percent: number, depositDue: string, balanceDue: string): ScheduleRow[] {
  const deposit = roundMoney((operatorTotal * percent) / 100);
  const balance = roundMoney(operatorTotal - deposit);
  const rows: ScheduleRow[] = [];
  if (deposit > 0) rows.push({ label: 'Deposit', amount: deposit, dueDate: depositDue });
  if (balance > 0) rows.push({ label: 'Balance', amount: balance, dueDate: balanceDue });
  return rows;
}

export function toInstallments(rows: ScheduleRow[]): InstallmentRequest[] {
  return rows.map(row => ({ label: row.label, amount: row.amount ?? 0, dueDate: row.dueDate }));
}

export function toRows(installments: LedgerInstallmentDto[]): ScheduleRow[] {
  return installments.map(i => ({ label: i.label, amount: i.amount, dueDate: i.dueDate }));
}

/** What the API will answer as the expected commission; null when it cannot be worked out yet. */
export function previewCommission(
  type: CommissionType | '', rate: number | null, base: CommissionBase,
  operatorTotal: number | null, customerSale: number | null, amount: number | null,
): number | null {
  if (type === '') return null;
  if (type === 'Fixed') return amount;
  const from = base === 'CustomerSale' ? customerSale : operatorTotal;
  return from === null || rate === null ? null : roundMoney((from * rate) / 100);
}

export function statusTone(status: LedgerReservationStatus): Tone {
  switch (status) {
    case 'Confirmed': return 'ok';
    case 'Completed': return 'brand';
    case 'Cancelled': return 'bad';
    default: return 'neutral';
  }
}

export function installmentTone(installment: Pick<LedgerInstallmentDto, 'status' | 'isOverdue'>): Tone {
  if (installment.status === 'Paid') return 'ok';
  if (installment.isOverdue) return 'bad';
  return installment.status === 'PartiallyPaid' ? 'warn' : 'neutral';
}

export function installmentStatusLabel(installment: Pick<LedgerInstallmentDto, 'status' | 'isOverdue'>): string {
  const status = t(`ledger.installmentStatus.${installment.status}`);
  return installment.isOverdue ? `${status} · ${t('ledger.installmentStatus.overdue').toLowerCase()}` : status;
}

const AUDIT_LOOK: Record<string, { icon: IconName; tone: TimelineItem['tone'] }> = {
  Created: { icon: 'plus', tone: 'neutral' },
  Updated: { icon: 'pencil', tone: 'neutral' },
  ScheduleReplaced: { icon: 'calendar', tone: 'info' },
  PaymentRecorded: { icon: 'card', tone: 'ok' },
  PaymentVoided: { icon: 'slash-circle', tone: 'bad' },
  StatusChanged: { icon: 'tag', tone: 'info' },
  CommissionReceived: { icon: 'percent', tone: 'ok' },
  DocumentUploaded: { icon: 'file', tone: 'neutral' },
  DocumentDeleted: { icon: 'x-circle', tone: 'warn' },
};

function auditDetail(event: LedgerAuditEventDto, currency: string): string | null {
  const changes = event.changes ?? {};
  const money = (value: unknown) => (typeof value === 'number' ? formatMoney(value, currency) : null);

  if (event.action === 'PaymentRecorded' || event.action === 'PaymentVoided') {
    const amount = money(changes['amount']?.to);
    return amount ? t('ledger.audit.amount', { amount }) : null;
  }
  if (event.action === 'StatusChanged') {
    const from = tOptional(`ledger.status.${String(changes['status']?.from)}`);
    const to = tOptional(`ledger.status.${String(changes['status']?.to)}`);
    return from && to ? t('ledger.audit.statusChange', { from: from.toLowerCase(), to: to.toLowerCase() }) : null;
  }
  if (event.action === 'Updated') {
    const from = money(changes['operatorTotal']?.from);
    const to = money(changes['operatorTotal']?.to);
    return from && to ? t('ledger.audit.priceChange', { from, to }) : null;
  }
  if (event.action === 'CommissionReceived') {
    const amount = money(changes['commissionReceived']?.to);
    return amount ? t('ledger.audit.amount', { amount }) : null;
  }
  if (event.action === 'DocumentUploaded' || event.action === 'DocumentDeleted') {
    const name = changes['fileName']?.to;
    return typeof name === 'string' ? name : null;
  }
  return null;
}

/** Audit events → timeline, newest first (the API already orders them). */
export function auditTimeline(events: LedgerAuditEventDto[], currency: string): TimelineItem[] {
  return events.map(event => {
    const look = AUDIT_LOOK[event.action] ?? { icon: 'info' as IconName, tone: 'neutral' as const };
    const actor = t(event.actorType === 'ApiClient' ? 'ledger.audit.actorApiClient' : 'ledger.audit.actorAgency');
    return {
      title: tOptional(`ledger.audit.${event.action}`) ?? event.action,
      detail: auditDetail(event, currency),
      meta: `${actor} · ${formatDateTime(event.occurredAt)}`,
      note: event.reason,
      icon: look.icon,
      tone: look.tone,
    };
  });
}
