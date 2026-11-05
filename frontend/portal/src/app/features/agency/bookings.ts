import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, resource, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { BookingDto, PassengerDto } from '../../core/api.types';
import { problemMessage } from '../../core/problem';
import { ViewportService } from '../../core/viewport';
import { OdDatePipe } from '../../shared/pipes/date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import {
  Banner, ButtonDirective, Card, Deadline, DialogService, EmptyState, Icon, PageHeader, SegmentOption, Segmented,
  Skeleton, StatusChip, ToastService,
} from '../../shared/ui';

/** Agencies may withdraw until money has been received; after that the operator decides. */
export function agencyCanCancel(b: Pick<BookingDto, 'status' | 'paidAmount'>): boolean {
  return (b.status === 'Draft' || b.status === 'PriceConfirmed' || b.status === 'AwaitingPayment')
    && b.paidAmount === 0;
}

type View = 'all' | 'due' | 'upcoming' | 'drafts' | 'closed';

const CLOSED = new Set(['Rejected', 'Expired', 'Cancelled', 'Failed']);

/** Saved views over the loaded list — a lens, not a query; the API returns every booking of the agency. */
export function matchesView(b: BookingDto, view: View, today: string): boolean {
  switch (view) {
    case 'all': return true;
    case 'due': return b.status === 'AwaitingPayment';
    case 'upcoming': return b.status === 'Confirmed' && b.checkIn >= today;
    case 'drafts': return b.status === 'Draft' || b.status === 'PriceConfirmed';
    case 'closed': return CLOSED.has(b.status);
  }
}

@Component({
  selector: 'app-agency-bookings',
  imports: [
    RouterLink, MoneyPipe, OdDatePipe,
    Banner, ButtonDirective, Card, Deadline, EmptyState, Icon, PageHeader, Segmented, Skeleton, StatusChip,
  ],
  templateUrl: './bookings.html',
  styleUrl: './bookings.scss',
})
export class AgencyBookings {
  private readonly http = inject(HttpClient);
  private readonly dialogs = inject(DialogService);
  private readonly toasts = inject(ToastService);
  private readonly router = inject(Router);

  readonly phone = inject(ViewportService).isPhone;
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly view = signal<View>('all');

  readonly bookings = resource({
    loader: () => firstValueFrom(this.http.get<BookingDto[]>('/api/v1/bookings')),
  });

  private readonly today = new Date().toISOString().slice(0, 10);

  readonly views = computed<SegmentOption[]>(() => {
    const list = this.bookings.value() ?? [];
    const count = (view: View) => list.filter(b => matchesView(b, view, this.today)).length;
    return [
      { value: 'all', label: 'All', count: list.length },
      { value: 'due', label: 'Needs payment', count: count('due') },
      { value: 'upcoming', label: 'Upcoming', count: count('upcoming') },
      { value: 'drafts', label: 'Drafts', count: count('drafts') },
      { value: 'closed', label: 'Closed' },
    ];
  });

  readonly filtered = computed(() =>
    (this.bookings.value() ?? []).filter(b => matchesView(b, this.view(), this.today)));

  cancellable(b: BookingDto): boolean {
    return agencyCanCancel(b);
  }

  lead(b: BookingDto): PassengerDto | undefined {
    return b.passengers.find(p => p.isLead) ?? b.passengers[0];
  }

  open(b: BookingDto): void {
    void this.router.navigate(['/agency/bookings', b.id]);
  }

  setView(view: string): void {
    this.view.set(view as View);
  }

  async cancel(b: BookingDto, event: Event): Promise<void> {
    event.stopPropagation();
    // Cancellation is terminal server-side — never fire it from a bare click.
    const decision = await this.dialogs.confirm({
      title: `Cancel booking ${b.ref}?`,
      message: 'The reservation is withdrawn and any supplier hold is released. This can’t be undone.',
      confirmLabel: 'Cancel booking',
      cancelLabel: 'Keep booking',
      destructive: true,
    });
    if (!decision) return;

    this.busy.set(true);
    this.error.set(null);
    try {
      await firstValueFrom(this.http.post(`/api/v1/bookings/${b.id}/cancel`, {}));
      this.toasts.show(`${b.ref} cancelled`);
      this.bookings.reload();
    } catch (err) {
      this.error.set(problemMessage(err, 'Cancellation failed.'));
    } finally {
      this.busy.set(false);
    }
  }
}
