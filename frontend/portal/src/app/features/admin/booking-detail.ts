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
import { problemMessage } from '../../core/problem';
import { OdDatePipe, OdDateTimePipe } from '../../shared/pipes/date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import {
  Banner, ButtonDirective, Card, Deadline, DialogService, EmptyState, Field, Icon, IconName, InputDirective,
  Masked, NextStep, PageHeader, Pill, Progress, SegmentOption, Segmented, Sheet, Skeleton, StatusChip, Timeline,
  ToastService,
  bookingStatusLabel,
} from '../../shared/ui';
import { operatorActions, parseBreakdown } from './booking-actions';
import { VAT_TREATMENT_LABELS, documentKindLabel, vatTreatmentShort } from './document-labels';

const PAYMENT_METHODS: SegmentOption[] = [
  { value: 'BankTransfer', label: 'Bank transfer' },
  { value: 'Card', label: 'Card' },
  { value: 'Cash', label: 'Cash' },
  { value: 'Other', label: 'Other' },
];

const METHOD_LABELS: Record<string, string> = {
  BankTransfer: 'Bank transfer', Card: 'Card', Cash: 'Cash', Other: 'Other',
};

interface NextAction {
  icon: IconName;
  tone: 'warn' | 'ok' | 'info' | 'neutral';
  title: string;
  sub: string;
  action?: { label: string; run: () => void; disabledReason: string | null };
}

/** One reservation from the operator's side: the net side, the money and the decisions. */
@Component({
  selector: 'app-admin-booking-detail',
  imports: [
    FormsModule, MoneyPipe, OdDatePipe, OdDateTimePipe,
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
    (this.booking()?.history ?? []).map(h => ({ ...h, actor: h.actorType }))));

  readonly subtitle = computed(() => {
    const b = this.booking();
    if (!b) return null;
    const agency = this.agency.value()?.name ?? 'Agency';
    const pax = `${b.adults} ${b.adults === 1 ? 'adult' : 'adults'}${b.children ? `, ${b.children} ${b.children === 1 ? 'child' : 'children'}` : ''}`;
    return `${agency} · ${b.hotelName} · ${b.programName} · ${pax} · created ${formatDateTime(b.createdAt)}`;
  });

  /** What the operator should do next, and why the button is locked if it is. */
  readonly next = computed<NextAction | null>(() => {
    const b = this.booking();
    const a = this.actions();
    if (!b || !a) return null;
    const money = (amount: number) => formatMoney(amount, b.currency);
    switch (b.status) {
      case 'AwaitingPayment':
        if (b.balanceDue > 0) {
          return {
            icon: 'card', tone: 'warn',
            title: `Collect ${money(b.balanceDue)}${b.paymentDueAt ? ` before ${formatDateTime(b.paymentDueAt)}` : ''}`,
            sub: b.paidAmount > 0 ? `${money(b.paidAmount)} received so far. Record the rest when it arrives.` : 'Nothing received yet. Record the payment when it arrives.',
            action: { label: 'Record payment', run: () => this.payOpen.set(true), disabledReason: null },
          };
        }
        return {
          icon: 'check-circle', tone: 'ok',
          title: 'Fully paid — ready to confirm',
          sub: 'Confirming makes the booking final and notifies the agency.',
          action: {
            label: 'Confirm booking', run: () => void this.confirm(),
            disabledReason: a.confirm ? null : 'Requires an operator admin',
          },
        };
      case 'Confirmed':
        if (this.documents.value() && !this.invoice()) {
          return {
            icon: 'file', tone: 'info', title: 'Issue the invoice',
            sub: 'Next number in the series. The agency can download it right away.',
            action: { label: 'Issue invoice', run: () => this.openIssue(), disabledReason: null },
          };
        }
        return { icon: 'check-circle', tone: 'ok', title: 'All done', sub: 'Confirmed, paid in full' + (this.invoice() ? ` and invoiced (${this.invoice()!.number})` : '') + '. Nothing else needs you.' };
      case 'PendingConfirmation':
        return { icon: 'hourglass', tone: 'info', title: 'Waiting for the supplier', sub: 'The hold request is in flight; the agency will see the payment deadline once it lands.' };
      case 'Draft':
      case 'PriceConfirmed':
        return { icon: 'pencil', tone: 'neutral', title: 'Not submitted yet', sub: 'The agency has not sent this booking to the supplier.' };
      default:
        return { icon: 'slash-circle', tone: 'neutral', title: `Closed · ${bookingStatusLabel(b.status)}`, sub: 'No operator action is available in this status.' };
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
      this.toasts.show(`Invoice ${doc.number} issued · available to the agency`);
      this.documents.reload();
    } catch (err) {
      this.error.set(problemMessage(err, 'The invoice could not be issued.'));
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
      this.toasts.show(`Download of ${doc.number} failed`, { tone: 'bad' });
    }
  }

  methodLabel(method: string): string {
    return METHOD_LABELS[method] ?? method;
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
      updated => `Payment ${recorded} recorded · balance ${formatMoney(updated.balanceDue, updated.currency)}`);
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
      title: `Confirm ${b.ref}?`,
      message: 'The agency is notified and the booking becomes final. After this, only an operator admin can cancel it.',
      confirmLabel: 'Confirm booking',
      cancelLabel: 'Not now',
      facts: [
        { label: 'Paid', value: `${formatMoney(b.paidAmount, b.currency)} of ${formatMoney(b.sellAmount, b.currency)}` },
        { label: 'Supplier hold', value: b.providerBookingRef ? `${b.providerBookingRef} · active` : '—' },
      ],
    });
    if (!decision) return;
    await this.act(`/api/v1/admin/bookings/${b.id}/confirm`, {}, () => `${b.ref} confirmed · agency notified`);
  }

  async reject(): Promise<void> {
    await this.decide('reject', 'Reject', 'The supplier hold is released and the agency is told why.', 'rejected');
  }

  async cancel(): Promise<void> {
    await this.decide('cancel', 'Cancel', 'The booking is cancelled and the agency is told why. This can’t be undone.', 'cancelled');
  }

  async expire(): Promise<void> {
    const b = this.booking();
    if (!b) return;
    const decision = await this.dialogs.confirm({
      title: `Expire ${b.ref}?`,
      message: 'The payment deadline has passed. The supplier hold is released and the agency is notified.',
      confirmLabel: 'Expire booking',
      destructive: true,
    });
    if (!decision) return;
    await this.act(`/api/v1/admin/bookings/${b.id}/expire`, {}, () => `${b.ref} expired`);
  }

  async voidPayment(payment: OperatorPayment): Promise<void> {
    const b = this.booking();
    if (!b) return;
    const decision = await this.dialogs.confirm({
      title: `Void payment of ${formatMoney(payment.amount, payment.currency)}?`,
      message: 'The payment stays on record as voided; the balance due goes back up.',
      confirmLabel: 'Void payment',
      destructive: true,
      reason: { label: 'Reason', placeholder: 'e.g. transfer bounced', required: true },
    });
    if (!decision) return;
    await this.act(`/api/v1/admin/bookings/${b.id}/payments/${payment.id}/void`, { reason: decision.reason },
      () => 'Payment voided');
  }

  private async decide(action: 'reject' | 'cancel', verb: string, consequence: string, past: string): Promise<void> {
    const b = this.booking();
    if (!b) return;
    const decision = await this.dialogs.confirm({
      title: `${verb} ${b.ref}?`,
      message: consequence,
      confirmLabel: `${verb} booking`,
      cancelLabel: 'Keep booking',
      destructive: true,
      reason: { label: 'Reason', placeholder: 'Visible to the agency', required: true },
    });
    if (!decision) return;
    await this.act(`/api/v1/admin/bookings/${b.id}/${action}`, { note: decision.reason }, () => `${b.ref} ${past}`);
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
      this.error.set(problemMessage(err, 'The action failed.'));
      return false;
    } finally {
      this.busy.set(false);
    }
  }
}
