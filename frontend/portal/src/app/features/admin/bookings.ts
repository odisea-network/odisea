import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, WritableSignal, computed, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AgencyDto, OperatorBookingSummary, PagedResult } from '../../core/api.types';
import { formatDate, formatDateTime, formatMoney } from '../../core/format';

export const BOOKING_STATUSES = [
  'Draft', 'PriceConfirmed', 'PendingConfirmation', 'AwaitingPayment',
  'Confirmed', 'Rejected', 'Expired', 'Cancelled', 'Failed',
] as const;

/** Operator oversight: every agency's reservations, filtered and paged by the API. */
@Component({
  selector: 'app-admin-bookings',
  imports: [FormsModule],
  templateUrl: './bookings.html',
  styles: `
    .card { background: var(--surface-1); border: 1px solid var(--border); border-radius: .75rem; }
    .filters { display: flex; flex-wrap: wrap; gap: 1rem; align-items: end; padding: 1rem 1.25rem; margin-bottom: 1rem; }
    .field { display: flex; flex-direction: column; gap: .3rem; }
    label { font-size: .8rem; font-weight: 600; }
    select, input { padding: .5rem; border: 1px solid var(--border); border-radius: .45rem; background: var(--surface-1); font: inherit; }
    table { width: 100%; border-collapse: collapse; }
    th, td { text-align: left; padding: .7rem 1rem; border-bottom: 1px solid var(--border); vertical-align: top; }
    th.num, td.num { text-align: right; }
    .strong { font-weight: 700; }
    .muted { color: var(--text-muted); font-size: .85rem; }
    .clickable { cursor: pointer; }
    .chip { padding: .2rem .55rem; border-radius: 1rem; font-size: .75rem; font-weight: 600; background: var(--surface-2); white-space: nowrap; }
    .chip.s-Confirmed { background: #e2f4e5; color: #1b6e2a; }
    .chip.s-AwaitingPayment { background: #fff4e0; color: #8a5a00; }
    .chip.s-Cancelled, .chip.s-Failed, .chip.s-Rejected, .chip.s-Expired { background: #fbe4e4; color: #a32020; }
    .pager { display: flex; gap: 1rem; align-items: center; justify-content: flex-end; padding: .75rem 1.25rem; }
    .error { color: var(--danger); }
  `,
})
export class AdminBookings {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  readonly statuses = BOOKING_STATUSES;
  readonly status = signal('');
  readonly agencyId = signal('');
  readonly from = signal('');
  readonly to = signal('');
  readonly page = signal(1);
  readonly pageSize = 25;

  readonly agencies = resource({
    loader: () => firstValueFrom(this.http.get<AgencyDto[]>('/api/v1/agencies')),
  });

  readonly agencyNames = computed(() =>
    new Map((this.agencies.value() ?? []).map(a => [a.id, a.name])));

  readonly bookings = resource({
    params: () => ({
      status: this.status(), agencyId: this.agencyId(), from: this.from(), to: this.to(), page: this.page(),
    }),
    loader: ({ params }) => {
      let query = new HttpParams().set('page', params.page).set('pageSize', this.pageSize);
      if (params.status) query = query.set('status', params.status);
      if (params.agencyId) query = query.set('agencyId', params.agencyId);
      if (params.from) query = query.set('from', params.from);
      if (params.to) query = query.set('to', params.to);
      return firstValueFrom(
        this.http.get<PagedResult<OperatorBookingSummary>>('/api/v1/admin/bookings', { params: query }));
    },
  });

  readonly pageCount = computed(() => {
    const result = this.bookings.value();
    return result ? Math.max(1, Math.ceil(result.totalCount / result.pageSize)) : 1;
  });

  readonly fmtMoney = formatMoney;
  readonly fmtDate = formatDate;
  readonly fmtDateTime = formatDateTime;

  agencyName(id: string): string {
    return this.agencyNames().get(id) ?? id.slice(0, 8);
  }

  /** Any filter change restarts paging from the first page. */
  filter(target: WritableSignal<string>, value: string): void {
    target.set(value);
    this.page.set(1);
  }

  open(b: OperatorBookingSummary): void {
    void this.router.navigate(['/admin/bookings', b.id]);
  }
}
