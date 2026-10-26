import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, input, resource, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { BookingDto, FinancialDocumentDto } from '../../core/api.types';
import { historyTimeline } from '../../core/booking-history';
import { BrandingService } from '../../core/branding.service';
import { formatDateTime, formatMoney } from '../../core/format';
import { problemMessage } from '../../core/problem';
import { OdDatePipe } from '../../shared/pipes/date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import {
  Banner, ButtonDirective, Card, Deadline, DialogService, EmptyState, Icon, IconName, Masked, NextStep,
  PageHeader, Pill, Progress, Skeleton, StatusChip, Timeline, ToastService, bookingStatusLabel,
} from '../../shared/ui';
import { agencyCanCancel } from './bookings';

interface NextAction {
  icon: IconName;
  tone: 'neutral' | 'ok' | 'warn' | 'info' | 'bad';
  title: string;
  sub: string;
  action?: { label: string; link: unknown[]; fragment?: string };
}

/** One reservation from the agency's side: what to do next, who travels, what to pay. */
@Component({
  selector: 'app-agency-booking-detail',
  imports: [
    RouterLink, MoneyPipe, OdDatePipe,
    Banner, ButtonDirective, Card, Deadline, EmptyState, Icon, Masked, NextStep, PageHeader, Pill, Progress,
    Skeleton, StatusChip, Timeline,
  ],
  templateUrl: './booking-detail.html',
  styleUrl: './booking-detail.scss',
})
export class AgencyBookingDetail {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly dialogs = inject(DialogService);
  private readonly toasts = inject(ToastService);
  protected readonly brand = inject(BrandingService);

  /** Bound from the route parameter by withComponentInputBinding(). */
  readonly id = input.required<string>();

  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly loaded = resource({
    params: () => ({ id: this.id() }),
    loader: ({ params }) => firstValueFrom(this.http.get<BookingDto>(`/api/v1/bookings/${params.id}`)),
  });

  readonly documents = resource({
    loader: () => firstValueFrom(this.http.get<FinancialDocumentDto[]>('/api/v1/agency/documents')),
  });

  readonly booking = computed(() => this.loaded.value() ?? null);
  readonly bookingDocuments = computed(() => {
    const ref = this.booking()?.ref;
    return (this.documents.value() ?? []).filter(d => d.bookingRef === ref);
  });
  readonly timeline = computed(() => historyTimeline(this.booking()?.history ?? []));
  readonly cancellable = computed(() => {
    const b = this.booking();
    return b ? agencyCanCancel(b) : false;
  });

  readonly subtitle = computed(() => {
    const b = this.booking();
    if (!b) return null;
    const lead = b.passengers.find(p => p.isLead);
    const count = b.passengers.length || b.adults + b.children;
    return `${b.hotelName} · ${b.programName} · ${count} ${count === 1 ? 'traveller' : 'travellers'}${lead ? ` · lead ${lead.firstName} ${lead.lastName}` : ''}`;
  });

  readonly next = computed<NextAction | null>(() => {
    const b = this.booking();
    if (!b) return null;
    switch (b.status) {
      case 'Draft':
      case 'PriceConfirmed':
        return {
          icon: 'pencil', tone: 'neutral', title: 'Finish this booking',
          sub: 'Travellers are saved. Continue to check the live price and submit.',
          action: { label: 'Continue booking', link: ['/agency/book'] },
        };
      case 'PendingConfirmation':
        return { icon: 'hourglass', tone: 'info', title: 'Waiting for the supplier', sub: 'The hold request is in flight. You will get a payment deadline once it lands.' };
      case 'AwaitingPayment':
        return {
          icon: 'card', tone: 'warn',
          title: `Pay ${formatMoney(b.balanceDue, b.currency)}${b.paymentDueAt ? ` by ${formatDateTime(b.paymentDueAt)}` : ''}`,
          sub: 'After the deadline the hold is released and the booking expires.',
          action: { label: 'Payment details', link: [], fragment: 'pay' },
        };
      case 'Confirmed':
        return { icon: 'check-circle', tone: 'ok', title: 'Confirmed', sub: 'Paid in full and final. Your invoice appears under Documents once issued.' };
      default:
        return { icon: 'slash-circle', tone: 'neutral', title: `Closed · ${bookingStatusLabel(b.status)}`, sub: 'Nothing more to do on this booking.' };
    }
  });

  async download(doc: FinancialDocumentDto): Promise<void> {
    try {
      const blob = await firstValueFrom(
        this.http.get(`/api/v1/agency/documents/${doc.id}/pdf`, { responseType: 'blob' }),
      );
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `${doc.kind}-${doc.number}.pdf`;
      anchor.click();
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch {
      this.toasts.show(`Download of ${doc.number} failed`, { tone: 'bad' });
    }
  }

  async cancel(): Promise<void> {
    const b = this.booking();
    if (!b) return;
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
      this.loaded.reload();
    } catch (err) {
      this.error.set(problemMessage(err, 'Cancellation failed.'));
    } finally {
      this.busy.set(false);
    }
  }

  continueDraft(): void {
    const b = this.booking();
    if (b) void this.router.navigate(['/agency/book'], { queryParams: { bookingId: b.id } });
  }
}
