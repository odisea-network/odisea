import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, input, resource, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { BookingDto, FinancialDocumentDto } from '../../core/api.types';
import { historyTimeline } from '../../core/booking-history';
import { BrandingService } from '../../core/branding.service';
import { formatDateTime, formatMoney } from '../../core/format';
import { t, tOptional } from '../../core/i18n/i18n';
import { problemMessage } from '../../core/problem';
import { OdDatePipe } from '../../shared/pipes/date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { TPipe } from '../../shared/pipes/t.pipe';
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
    RouterLink, MoneyPipe, OdDatePipe, TPipe,
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
    const party = t(count === 1 ? 'bookingDetail.passengersOne' : 'bookingDetail.passengersMany', { n: count });
    const leadText = lead ? ` · ${t('bookingDetail.leadIs', { name: `${lead.firstName} ${lead.lastName}` })}` : '';
    return `${b.hotelName} · ${b.programName} · ${party}${leadText}`;
  });

  readonly next = computed<NextAction | null>(() => {
    const b = this.booking();
    if (!b) return null;
    switch (b.status) {
      case 'Draft':
      case 'PriceConfirmed':
        return {
          icon: 'pencil', tone: 'neutral', title: t('bookingDetail.next.draftTitle'),
          sub: t('bookingDetail.next.draftSub'),
          action: { label: t('bookingDetail.next.draftAction'), link: ['/agency/book'] },
        };
      case 'PendingConfirmation':
        return { icon: 'hourglass', tone: 'info', title: t('bookingDetail.next.pendingTitle'), sub: t('bookingDetail.next.pendingSub') };
      case 'AwaitingPayment':
        return {
          icon: 'card', tone: 'warn',
          title: b.paymentDueAt
            ? t('bookingDetail.next.payTitleBy', { amount: formatMoney(b.balanceDue, b.currency), deadline: formatDateTime(b.paymentDueAt) })
            : t('bookingDetail.next.payTitle', { amount: formatMoney(b.balanceDue, b.currency) }),
          sub: t('bookingDetail.next.paySub'),
          action: { label: t('bookingDetail.next.payAction'), link: [], fragment: 'pay' },
        };
      case 'Confirmed':
        return { icon: 'check-circle', tone: 'ok', title: t('bookingDetail.next.confirmedTitle'), sub: t('bookingDetail.next.confirmedSub') };
      default:
        return { icon: 'slash-circle', tone: 'neutral', title: t('bookingDetail.next.closedTitle', { status: bookingStatusLabel(b.status) }), sub: t('bookingDetail.next.closedSub') };
    }
  });

  passengerType(type: string | null): string {
    return type ? tOptional(`passenger.type.${type}`) ?? type : '—';
  }

  documentType(type: string): string {
    if (type === 'Passport') return t('book.passport');
    return type === 'IdCard' ? t('book.idCard') : type;
  }

  kindLabel(kind: string): string {
    return tOptional(`agencyDocuments.kind.${kind}`) ?? kind;
  }

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
      this.toasts.show(t('agencyDocuments.downloadFailed', { number: doc.number }), { tone: 'bad' });
    }
  }

  async cancel(): Promise<void> {
    const b = this.booking();
    if (!b) return;
    const decision = await this.dialogs.confirm({
      title: t('bookings.cancelTitle', { ref: b.ref }),
      message: t('bookings.cancelMessage'),
      confirmLabel: t('bookings.cancelConfirm'),
      cancelLabel: t('bookings.cancelKeep'),
      destructive: true,
    });
    if (!decision) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      await firstValueFrom(this.http.post(`/api/v1/bookings/${b.id}/cancel`, {}));
      this.toasts.show(t('bookings.cancelled', { ref: b.ref }));
      this.loaded.reload();
    } catch (err) {
      this.error.set(problemMessage(err, t('bookings.cancelFailed')));
    } finally {
      this.busy.set(false);
    }
  }

  continueDraft(): void {
    const b = this.booking();
    if (b) void this.router.navigate(['/agency/book'], { queryParams: { bookingId: b.id } });
  }
}
