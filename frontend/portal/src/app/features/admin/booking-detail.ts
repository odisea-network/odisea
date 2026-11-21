import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, input, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import {
  AgencyDto, OperatorBooking, OperatorDocumentDto, OperatorPayment, PaymentMethod, RecordPaymentRequest,
} from '../../core/api.types';
import { AuthService } from '../../core/auth.service';
import { historyTimeline } from '../../core/booking-history';
import { formatDateTime, formatMoney } from '../../core/format';
import { t, tOptional } from '../../core/i18n/i18n';
import { problemMessage } from '../../core/problem';
import { OdDatePipe, OdDateTimePipe } from '../../shared/pipes/date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { TPipe } from '../../shared/pipes/t.pipe';
import {
  Banner, ButtonDirective, Card, Deadline, DialogService, EmptyState, Field, Icon, IconName, InputDirective,
  Masked, NextStep, PageHeader, Pill, Progress, SegmentOption, Segmented, Sheet, Skeleton, StatusChip, Timeline,
  ToastService,
  bookingStatusLabel,
} from '../../shared/ui';
import { operatorActions, parseBreakdown } from './booking-actions';
import { VAT_TREATMENT_LABELS, documentKindLabel, vatTreatmentShort } from './document-labels';

const PAYMENT_METHODS: SegmentOption[] = [
  { value: 'BankTransfer', label: t('ledger.method.BankTransfer') },
  { value: 'Card', label: t('ledger.method.Card') },
  { value: 'Cash', label: t('ledger.method.Cash') },
  { value: 'Other', label: t('ledger.method.Other') },
];

interface NextAction {
  icon: IconName;
  tone: 'warn' | 'ok' | 'info' | 'neutral' | 'bad';
  title: string;
  sub: string;
  action?: { label: string; run: () => void; disabledReason: string | null };
}

/** One reservation from the operator's side: the net side, the money and the decisions. */
@Component({
  selector: 'app-admin-booking-detail',
  imports: [
    FormsModule, MoneyPipe, OdDatePipe, OdDateTimePipe, TPipe,
    Banner, ButtonDirective, Card, Deadline, EmptyState, Field, Icon, InputDirective, Masked, NextStep, PageHeader,
    Pill, Progress, Segmented, Sheet, Skeleton, StatusChip, Timeline,
  ],
  templateUrl: './booking-detail.html',
  styleUrl: './booking-detail.scss',
})
export class AdminBookingDetail {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly dialogs = inject(DialogService);
  private readonly toasts = inject(ToastService);

  /** Bound from the route parameter by withComponentInputBinding(). */
  readonly id = input.required<string>();

  readonly booking = signal<OperatorBooking | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly payOpen = signal(false);
  readonly issueOpen = signal(false);

  readonly loaded = resource({
    params: () => ({ id: this.id() }),
    loader: async ({ params }) => {
      const booking = await firstValueFrom(
        this.http.get<OperatorBooking>(`/api/v1/admin/bookings/${params.id}`));
      this.booking.set(booking);
      this.paymentAmount = booking.balanceDue;
      this.paymentCurrency = booking.currency;
      return booking;
    },
  });

  readonly agency = resource({
    params: () => {
      const agencyId = this.booking()?.agencyId;
      return agencyId ? { agencyId } : undefined;
    },
    loader: ({ params }) => firstValueFrom(this.http.get<AgencyDto>(`/api/v1/agencies/${params.agencyId}`)),
  });

  /** Latest 200 documents, narrowed to this booking by its reference. */
  readonly documents = resource({
    params: () => {
      const ref = this.booking()?.ref;
      return ref ? { ref } : undefined;
    },
    loader: async ({ params }) => (await firstValueFrom(this.http.get<OperatorDocumentDto[]>('/api/v1/documents')))
      .filter(d => d.bookingRef === params.ref),
  });
  readonly invoice = computed(() =>
    (this.documents.value() ?? []).find(d => d.kind === 'Invoice' && d.status === 'Issued') ?? null);

  readonly vatOptions = Object.entries(VAT_TREATMENT_LABELS).map(([value, label]) => ({ value, label }));
  readonly kindLabel = documentKindLabel;
  readonly vatLabel = vatTreatmentShort;
  invoiceForm = { vatTreatment: 'MarginScheme', exemptionGrounds: '', dueInDays: 14 };

  readonly isAdmin = computed(() => this.auth.user()?.role === 'Admin');
  readonly actions = computed(() => {
    const b = this.booking();
    return b ? operatorActions(b, this.isAdmin(), new Date()) : null;
  });
  readonly breakdown = computed(() => parseBreakdown(this.booking()?.breakdown));
  readonly timeline = computed(() => historyTimeline(
    (this.booking()?.history ?? []).map(h => ({ ...h, actor: tOptional(`adminBookingDetail.actor.${h.actorType}`) ?? h.actorType }))));

  readonly subtitle = computed(() => {
    const b = this.booking();
    if (!b) return null;
    const agency = this.agency.value()?.name ?? t('adminBookingDetail.agencyFallback');
    const adults = t(b.adults === 1 ? 'common.adultOne' : 'common.adultMany', { n: b.adults });
    const children = b.children ? `, ${t(b.children === 1 ? 'common.childOne' : 'common.childMany', { n: b.children })}` : '';
    const created = t('adminBookingDetail.createdAt', { date: formatDateTime(b.createdAt) });
    return `${agency} · ${b.hotelName} · ${b.programName} · ${adults}${children} · ${created}`;
  });

  /** What the operator should do next, and why the button is locked if it is. */
  readonly next = computed<NextAction | null>(() => {
    const b = this.booking();
    const a = this.actions();
    if (!b || !a) return null;
    const money = (amount: number) => formatMoney(amount, b.currency);
    // A hold the supplier still keeps for a closed booking costs money: it comes first.
    if (b.providerRelease === 'Failed') {
      return {
        icon: 'alert-triangle', tone: 'bad',
        title: t('adminBookingDetail.next.releaseFailedTitle'),
        sub: t('adminBookingDetail.next.releaseFailedSub', {
          provider: b.providerCode, reason: b.providerReleaseError ?? t('adminBookingDetail.next.noReason'),
        }),
        action: { label: t('adminBookingDetail.next.retryRelease'), run: () => void this.retryRelease(), disabledReason: null },
      };
    }
    if (b.providerRelease === 'Pending') {
      return {
        icon: 'refresh', tone: 'warn',
        title: t('adminBookingDetail.next.releasePendingTitle'),
        sub: b.providerReleaseError
          ? t(b.providerReleaseAttempts === 1 ? 'adminBookingDetail.next.releaseRetriedOne' : 'adminBookingDetail.next.releaseRetriedMany',
            { error: b.providerReleaseError, n: b.providerReleaseAttempts })
          : t('adminBookingDetail.next.releasePendingSub'),
        action: { label: t('adminBookingDetail.next.retryRelease'), run: () => void this.retryRelease(), disabledReason: null },
      };
    }
    switch (b.status) {
      case 'AwaitingPayment':
        if (b.balanceDue > 0) {
          return {
            icon: 'card', tone: 'warn',
            title: b.paymentDueAt
              ? t('adminBookingDetail.next.collectTitleBy', { amount: money(b.balanceDue), deadline: formatDateTime(b.paymentDueAt) })
              : t('adminBookingDetail.next.collectTitle', { amount: money(b.balanceDue) }),
            sub: b.paidAmount > 0
              ? t('adminBookingDetail.next.collectSubPartial', { amount: money(b.paidAmount) })
              : t('adminBookingDetail.next.collectSubNone'),
            action: { label: t('adminBookingDetail.recordPayment'), run: () => this.payOpen.set(true), disabledReason: null },
          };
        }
        return {
          icon: 'check-circle', tone: 'ok',
          title: t('adminBookingDetail.next.paidTitle'),
          sub: t('adminBookingDetail.next.paidSub'),
          action: {
            label: t('adminBookingDetail.confirmBooking'), run: () => void this.confirm(),
            disabledReason: a.confirm ? null : t('adminBookingDetail.next.adminRequired'),
          },
        };
      case 'Confirmed':
        if (this.documents.value() && !this.invoice()) {
          return {
            icon: 'file', tone: 'info', title: t('adminBookingDetail.next.invoiceTitle'),
            sub: t('adminBookingDetail.next.invoiceSub'),
            action: { label: t('adminBookingDetail.issueInvoice'), run: () => this.openIssue(), disabledReason: null },
          };
        }
        return {
          icon: 'check-circle', tone: 'ok', title: t('adminBookingDetail.next.doneTitle'),
          sub: this.invoice()
            ? t('adminBookingDetail.next.doneSubInvoiced', { number: this.invoice()!.number })
            : t('adminBookingDetail.next.doneSub'),
        };
      case 'PendingConfirmation':
        return { icon: 'hourglass', tone: 'info', title: t('adminBookingDetail.next.pendingTitle'), sub: t('adminBookingDetail.next.pendingSub') };
      case 'Draft':
      case 'PriceConfirmed':
        return { icon: 'pencil', tone: 'neutral', title: t('adminBookingDetail.next.draftTitle'), sub: t('adminBookingDetail.next.draftSub') };
      default:
        return {
          icon: 'slash-circle', tone: 'neutral',
          title: t('bookingDetail.next.closedTitle', { status: bookingStatusLabel(b.status) }),
          sub: t('adminBookingDetail.next.closedSub'),
        };
    }
  });

  // Payment form. The amount is prefilled with the API's balance due — never computed here.
  paymentAmount = 0;
  paymentCurrency = 'EUR';
  paymentMethod: PaymentMethod = 'BankTransfer';
  paymentReceivedOn = new Date().toISOString().slice(0, 10);
  paymentReference = '';
  paymentNote = '';

  readonly paymentMethods = PAYMENT_METHODS;

  openIssue(): void {
    this.invoiceForm = { vatTreatment: 'MarginScheme', exemptionGrounds: '', dueInDays: 14 };
    this.error.set(null);
    this.issueOpen.set(true);
  }

  invoiceValid(): boolean {
    const f = this.invoiceForm;
    return f.dueInDays >= 0 && f.dueInDays <= 60 && (f.vatTreatment !== 'Exempt' || !!f.exemptionGrounds.trim());
  }

  async issueInvoice(): Promise<void> {
    const b = this.booking();
    if (!b || !this.invoiceValid()) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      const doc = await firstValueFrom(this.http.post<OperatorDocumentDto>('/api/v1/documents/invoices', {
        bookingId: b.id, vatTreatment: this.invoiceForm.vatTreatment,
        exemptionGrounds: this.invoiceForm.exemptionGrounds.trim() || null, dueInDays: Number(this.invoiceForm.dueInDays),
      }));
      this.issueOpen.set(false);
      this.toasts.show(t('adminBookingDetail.invoiceIssued', { number: doc.number }));
      this.documents.reload();
    } catch (err) {
      this.error.set(problemMessage(err, t('adminBookingDetail.invoiceFailed')));
    } finally {
      this.busy.set(false);
    }
  }

  async downloadDocument(doc: OperatorDocumentDto): Promise<void> {
    try {
      const blob = await firstValueFrom(this.http.get(`/api/v1/documents/${doc.id}/pdf`, { responseType: 'blob' }));
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

  methodLabel(method: string): string {
    return tOptional(`ledger.method.${method}`) ?? method;
  }

  stepKindLabel(kind: string): string {
    return tOptional(`pricing.kind.${kind}`) ?? kind;
  }

  passengerKind(p: OperatorBooking['passengers'][number]): string {
    const type = p.passengerType ? tOptional(`passenger.type.${p.passengerType}`) ?? p.passengerType : '—';
    const gender = p.gender === 'Male' ? t('book.male') : p.gender === 'Female' ? t('book.female') : p.gender;
    return gender ? `${type} · ${gender}` : type;
  }

  documentTypeLabel(type: string): string {
    return type === 'IdCard' ? t('book.idCard') : type === 'Passport' ? t('book.passport') : type;
  }

  setMethod(value: string): void {
    this.paymentMethod = value as PaymentMethod;
  }

  async recordPayment(): Promise<void> {
    const b = this.booking();
    if (!b) return;
    const request: RecordPaymentRequest = {
      amount: this.paymentAmount,
      currency: this.paymentCurrency,
      method: this.paymentMethod,
      receivedOn: this.paymentReceivedOn,
      reference: this.paymentReference.trim() || null,
      note: this.paymentNote.trim() || null,
    };
    const recorded = formatMoney(request.amount, request.currency);
    const ok = await this.act(`/api/v1/admin/bookings/${b.id}/payments`, request,
      updated => t('adminBookingDetail.paymentRecorded', { amount: recorded, balance: formatMoney(updated.balanceDue, updated.currency) }));
    if (ok) {
      this.payOpen.set(false);
      this.paymentReference = '';
      this.paymentNote = '';
    }
  }

  async confirm(): Promise<void> {
    const b = this.booking();
    if (!b) return;
    const decision = await this.dialogs.confirm({
      title: t('adminBookingDetail.confirmTitle', { ref: b.ref }),
      message: t('adminBookingDetail.confirmMessage'),
      confirmLabel: t('adminBookingDetail.confirmBooking'),
      cancelLabel: t('adminBookingDetail.notNow'),
      facts: [
        {
          label: t('adminBookingDetail.paid'),
          value: t('bookingDetail.paidOf', { paid: formatMoney(b.paidAmount, b.currency), total: formatMoney(b.sellAmount, b.currency) }),
        },
        {
          label: t('adminBookingDetail.factHold'),
          value: b.providerBookingRef ? t('adminBookingDetail.holdActive', { ref: b.providerBookingRef }) : '—',
        },
      ],
    });
    if (!decision) return;
    await this.act(`/api/v1/admin/bookings/${b.id}/confirm`, {}, () => t('adminBookingDetail.confirmed', { ref: b.ref }));
  }

  async reject(): Promise<void> {
    const ref = this.booking()?.ref ?? '';
    await this.decide('reject', {
      title: t('adminBookingDetail.rejectTitle', { ref }), message: t('adminBookingDetail.rejectMessage'),
      confirmLabel: t('adminBookingDetail.rejectConfirm'), done: t('adminBookingDetail.rejected', { ref }),
    });
  }

  async cancel(): Promise<void> {
    const ref = this.booking()?.ref ?? '';
    await this.decide('cancel', {
      title: t('bookings.cancelTitle', { ref }), message: t('adminBookingDetail.cancelMessage'),
      confirmLabel: t('bookings.cancelConfirm'), done: t('bookings.cancelled', { ref }),
    });
  }

  async expire(): Promise<void> {
    const b = this.booking();
    if (!b) return;
    const decision = await this.dialogs.confirm({
      title: t('adminBookingDetail.expireTitle', { ref: b.ref }),
      message: t('adminBookingDetail.expireMessage'),
      confirmLabel: t('adminBookingDetail.expireConfirm'),
      destructive: true,
    });
    if (!decision) return;
    await this.act(`/api/v1/admin/bookings/${b.id}/expire`, {}, () => t('adminBookingDetail.expired', { ref: b.ref }));
  }

  async retryRelease(): Promise<void> {
    const b = this.booking();
    if (!b) return;
    await this.act(`/api/v1/admin/bookings/${b.id}/provider-release/retry`, {}, updated =>
      updated.providerRelease === 'Released'
        ? t('adminBookingDetail.released', { ref: b.ref })
        : t('adminBookingDetail.notReleased', { ref: b.ref }));
  }

  async voidPayment(payment: OperatorPayment): Promise<void> {
    const b = this.booking();
    if (!b) return;
    const decision = await this.dialogs.confirm({
      title: t('adminBookingDetail.voidTitle', { amount: formatMoney(payment.amount, payment.currency) }),
      message: t('adminBookingDetail.voidMessage'),
      confirmLabel: t('adminBookingDetail.voidConfirm'),
      destructive: true,
      reason: { label: t('adminBookingDetail.reason'), placeholder: t('adminBookingDetail.voidPlaceholder'), required: true },
    });
    if (!decision) return;
    await this.act(`/api/v1/admin/bookings/${b.id}/payments/${payment.id}/void`, { reason: decision.reason },
      () => t('adminBookingDetail.voidDone'));
  }

  private async decide(
    action: 'reject' | 'cancel',
    text: { title: string; message: string; confirmLabel: string; done: string },
  ): Promise<void> {
    const b = this.booking();
    if (!b) return;
    const decision = await this.dialogs.confirm({
      title: text.title,
      message: text.message,
      confirmLabel: text.confirmLabel,
      cancelLabel: t('bookings.cancelKeep'),
      destructive: true,
      reason: { label: t('adminBookingDetail.reason'), placeholder: t('adminBookingDetail.reasonVisible'), required: true },
    });
    if (!decision) return;
    await this.act(`/api/v1/admin/bookings/${b.id}/${action}`, { note: decision.reason }, () => text.done);
  }

  private async act(url: string, body: unknown, done: (updated: OperatorBooking) => string): Promise<boolean> {
    this.busy.set(true);
    this.error.set(null);
    try {
      const updated = await firstValueFrom(this.http.post<OperatorBooking>(url, body));
      this.booking.set(updated);
      this.paymentAmount = updated.balanceDue;
      this.toasts.show(done(updated));
      return true;
    } catch (err) {
      this.error.set(problemMessage(err, t('adminBookingDetail.actionFailed')));
      return false;
    } finally {
      this.busy.set(false);
    }
  }
}
