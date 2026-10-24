import { HttpClient } from '@angular/common/http';
import { Component, inject, resource, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { BookingDto } from '../../core/api.types';
import { formatDate, formatDateTime, formatMoney } from '../../core/format';
import { problemMessage } from '../../core/problem';

/** Agencies may withdraw until money has been received; after that the operator decides. */
export function agencyCanCancel(b: Pick<BookingDto, 'status' | 'paidAmount'>): boolean {
  return (b.status === 'Draft' || b.status === 'PriceConfirmed' || b.status === 'AwaitingPayment')
    && b.paidAmount === 0;
}

const STATUS_LABELS: Record<string, string> = {
  Draft: 'Draft',
  PriceConfirmed: 'Price confirmed',
  PendingConfirmation: 'Sent to supplier',
  AwaitingPayment: 'Awaiting payment',
  Confirmed: 'Confirmed',
  Rejected: 'Rejected',
  Expired: 'Expired',
  Cancelled: 'Cancelled',
  Failed: 'Failed',
};

@Component({
  selector: 'app-agency-bookings',
  templateUrl: './bookings.html',
  styles: `
    .card { background: var(--surface-1); border: 1px solid var(--border); border-radius: .75rem; width: 100%; border-collapse: collapse; }
    th, td { text-align: left; padding: .7rem 1rem; border-bottom: 1px solid var(--border); vertical-align: top; }
    th.num, td.num { text-align: right; }
    .strong { font-weight: 700; }
    .muted { color: var(--text-muted); font-size: .85rem; }
    .clickable { cursor: pointer; }
    .chip { padding: .2rem .55rem; border-radius: 1rem; font-size: .75rem; font-weight: 600; background: var(--surface-2); white-space: nowrap; }
    .chip.s-Confirmed { background: #e2f4e5; color: #1b6e2a; }
    .chip.s-AwaitingPayment { background: #fff4e0; color: #8a5a00; }
    .chip.s-Cancelled, .chip.s-Failed, .chip.s-Rejected, .chip.s-Expired { background: #fbe4e4; color: #a32020; }
    .detail td { background: var(--surface-2); }
    .columns { display: flex; gap: 3rem; flex-wrap: wrap; }
    h4 { margin: .2rem 0 .4rem; }
    .error { color: var(--danger); }
  `,
})
export class AgencyBookings {
  private readonly http = inject(HttpClient);

  readonly expanded = signal<string | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly bookings = resource({
    loader: () => firstValueFrom(this.http.get<BookingDto[]>('/api/v1/bookings')),
  });

  readonly fmtMoney = formatMoney;
  readonly fmtDate = formatDate;
  readonly fmtDateTime = formatDateTime;

  cancellable(b: BookingDto): boolean {
    return agencyCanCancel(b);
  }

  statusLabel(status: string): string {
    return STATUS_LABELS[status] ?? status;
  }

  toggle(id: string): void {
    this.expanded.set(this.expanded() === id ? null : id);
  }

  async cancel(b: BookingDto, event: Event): Promise<void> {
    event.stopPropagation();
    // Cancellation is terminal server-side — never fire it from a bare click.
    if (!window.confirm(`Cancel reservation ${b.ref}? This cannot be undone.`)) return;

    this.busy.set(true);
    this.error.set(null);
    try {
      await firstValueFrom(this.http.post(`/api/v1/bookings/${b.id}/cancel`, {}));
      this.bookings.reload();
    } catch (err) {
      this.error.set(problemMessage(err, 'Cancellation failed.'));
    } finally {
      this.busy.set(false);
    }
  }
}
