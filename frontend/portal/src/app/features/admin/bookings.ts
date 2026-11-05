import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, WritableSignal, computed, effect, inject, input, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AgencyDto, OperatorBookingSummary, PagedResult } from '../../core/api.types';
import { OdDatePipe } from '../../shared/pipes/date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import {
  ButtonDirective, Card, Deadline, EmptyState, InputDirective, PageHeader, Pager, Skeleton, StatusChip,
  bookingStatusLabel,
} from '../../shared/ui';

export const BOOKING_STATUSES = [
  'Draft', 'PriceConfirmed', 'PendingConfirmation', 'AwaitingPayment',
  'Confirmed', 'Rejected', 'Expired', 'Cancelled', 'Failed',
] as const;

/** Operator oversight: every agency's reservations, filtered and paged by the API. */
@Component({
  selector: 'app-admin-bookings',
  imports: [
    FormsModule, RouterLink, MoneyPipe, OdDatePipe,
    ButtonDirective, Card, Deadline, EmptyState, InputDirective, PageHeader, Pager, Skeleton, StatusChip,
  ],
  templateUrl: './bookings.html',
  styleUrl: './bookings.scss',
})
export class AdminBookings {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  /** Query parameter, e.g. from an agency page: /admin/bookings?agencyId=… */
  readonly agencyFilter = input<string | undefined>(undefined, { alias: 'agencyId' });
  readonly statusFilter = input<string | undefined>(undefined, { alias: 'status' });

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

  readonly hasFilters = computed(() => !!(this.status() || this.agencyId() || this.from() || this.to()));

  constructor() {
    effect(() => {
      const preset = this.agencyFilter();
      if (preset) this.filter(this.agencyId, preset);
    });
    effect(() => {
      const preset = this.statusFilter();
      if (preset) this.filter(this.status, preset);
    });
  }

  readonly statusLabel = bookingStatusLabel;

  agencyName(id: string): string {
    return this.agencyNames().get(id) ?? id.slice(0, 8);
  }

  /** Any filter change restarts paging from the first page. */
  filter(target: WritableSignal<string>, value: string): void {
    target.set(value);
    this.page.set(1);
  }

  clearFilters(): void {
    this.status.set('');
    this.agencyId.set('');
    this.from.set('');
    this.to.set('');
    this.page.set(1);
  }

  open(b: OperatorBookingSummary): void {
    void this.router.navigate(['/admin/bookings', b.id]);
  }
}
