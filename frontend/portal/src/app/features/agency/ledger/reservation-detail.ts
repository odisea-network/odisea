import { Component, computed, inject, input, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  LedgerInstallmentDto, LedgerPaymentDto, LedgerPaymentMethod, LedgerReservationDto, LedgerReservationStatus,
} from '../../../core/api.types';
import { AuthService } from '../../../core/auth.service';
import { formatMoney } from '../../../core/format';
import { t } from '../../../core/i18n/i18n';
import { localizedProblem, problemCode } from '../../../core/problem';
import { OdDatePipe } from '../../../shared/pipes/date.pipe';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';
import { TPipe } from '../../../shared/pipes/t.pipe';
import {
  Banner, ButtonDirective, Card, Crumb, DialogService, EmptyState, Field, Icon, InputDirective, PageHeader, Pill,
  Sheet, Skeleton, Timeline, ToastService,
} from '../../../shared/ui';
import { LedgerApi } from './ledger-api';
import {
  ScheduleRow, auditTimeline, installmentStatusLabel, installmentTone, isoToday, roundMoney, scheduleValid,
  statusTone, toInstallments, toRows,
} from './ledger.model';
import { LedgerScheduleEditor } from './schedule-editor';

const METHODS: LedgerPaymentMethod[] = ['BankTransfer', 'Card', 'Cash', 'Other'];

interface StatusAction {
  status: LedgerReservationStatus;
  label: string;
  danger?: boolean;
}

/** What the next status can be, given the current one. */
export function statusActions(status: LedgerReservationStatus): StatusAction[] {
  const cancel: StatusAction = { status: 'Cancelled', label: t('ledger.detail.cancel'), danger: true };
  switch (status) {
    case 'Draft': return [{ status: 'Confirmed', label: t('ledger.detail.markConfirmed') }, cancel];
    case 'Confirmed': return [{ status: 'Completed', label: t('ledger.detail.markCompleted') }, cancel];
    default: return [{ status: 'Confirmed', label: t('ledger.detail.reopen') }];
  }
}

/** The amount a new payment most likely is: what the first open instalment still needs, else everything owed. */
export function suggestedPayment(r: Pick<LedgerReservationDto, 'installments' | 'outstanding'>): number {
  return r.installments.find(i => i.remainingAmount > 0)?.remainingAmount ?? r.outstanding;
}

@Component({
  selector: 'app-ledger-reservation-detail',
  imports: [
    FormsModule, RouterLink, MoneyPipe, OdDatePipe, TPipe,
    Banner, ButtonDirective, Card, EmptyState, Field, Icon, InputDirective, PageHeader, Pill, Sheet, Skeleton, Timeline,
    LedgerScheduleEditor,
  ],
  templateUrl: './reservation-detail.html',
  styleUrl: './reservation-detail.scss',
})
export class LedgerReservationDetail {
  private readonly api = inject(LedgerApi);
  private readonly dialogs = inject(DialogService);
  private readonly toasts = inject(ToastService);
  private readonly auth = inject(AuthService);

  readonly id = input.required<string>();

  protected readonly methods = METHODS;
  protected readonly today = isoToday();
  protected readonly crumbs: Crumb[] = [{ label: t('ledger.detail.crumb'), link: '/agency/ledger/reservations' }];
  protected readonly isAdmin = computed(() => this.auth.user()?.role === 'AgencyAdmin');

  protected readonly loaded = resource({
    params: () => this.id(),
    loader: ({ params }) => this.api.get(params),
  });
  protected readonly history = resource({
    params: () => this.id(),
    loader: ({ params }) => this.api.history(params),
  });

  protected readonly reservation = computed(() => this.loaded.value() ?? null);
  protected readonly hasPayments = computed(() => (this.reservation()?.payments ?? []).some(p => !p.isVoided));
  protected readonly actions = computed(() => statusActions(this.reservation()?.status ?? 'Confirmed'));
  protected readonly timeline = computed(() =>
    auditTimeline(this.history.value() ?? [], this.reservation()?.currency ?? 'EUR'));

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly paymentOpen = signal(false);
  protected payment = this.newPayment(0);
  protected readonly paymentError = signal<string | null>(null);

  protected readonly scheduleOpen = signal(false);
  protected readonly scheduleRows = signal<ScheduleRow[]>([]);
  protected scheduleReason = '';
  protected readonly scheduleError = signal<string | null>(null);

  protected receivedDraft: number | null = null;

  protected readonly statusTone = statusTone;
  protected readonly installmentTone = installmentTone;
  protected readonly installmentStatus = installmentStatusLabel;

  protected statusLabel(status: LedgerReservationStatus): string {
    return t(`ledger.status.${status}`);
  }

  protected labelText(installment: LedgerInstallmentDto): string {
    return t(`ledger.installmentLabel.${installment.label}`);
  }

  protected methodText(method: LedgerPaymentMethod): string {
    return t(`ledger.method.${method}`);
  }

  protected commissionRule(r: LedgerReservationDto): string {
    const c = r.commission;
    if (!c) return '';
    if (c.type === 'Fixed' || c.base === 'Manual') return t('ledger.detail.commissionFixed');
    return t('ledger.detail.commissionRule', {
      rate: c.rate ?? 0,
      base: t(c.base === 'CustomerSale' ? 'ledger.form.baseCustomerSale' : 'ledger.form.baseOperatorTotal'),
    });
  }

  protected commissionStatus(r: LedgerReservationDto): string {
    const status = r.commission?.status;
    return status ? t(`ledger.commissionStatus.${status}`) : '';
  }

  protected overpayBy(r: LedgerReservationDto): number {
    return roundMoney((this.payment.amount ?? 0) - r.outstanding);
  }

  protected paymentValid(): boolean {
    return (this.payment.amount ?? 0) > 0 && !!this.payment.paidOn && this.payment.paidOn <= this.today;
  }

  protected openPayment(r: LedgerReservationDto): void {
    this.payment = this.newPayment(suggestedPayment(r));
    this.paymentError.set(null);
    this.paymentOpen.set(true);
  }

  protected async savePayment(r: LedgerReservationDto): Promise<void> {
    if (!this.paymentValid()) {
      this.paymentError.set(t('ledger.payment.invalid'));
      return;
    }
    const p = this.payment;
    const saved = await this.run(() => this.api.recordPayment(r.id, {
      amount: p.amount ?? 0, currency: r.currency, paidOn: p.paidOn, method: p.method,
      reference: p.reference.trim() || null, note: p.note.trim() || null, idempotencyKey: p.key,
    }), this.paymentError);
    if (!saved) return;
    this.paymentOpen.set(false);
    this.toasts.show(t('ledger.payment.saved', {
      amount: formatMoney(p.amount, r.currency), outstanding: formatMoney(saved.outstanding, saved.currency),
    }));
  }

  protected async voidPayment(r: LedgerReservationDto, p: LedgerPaymentDto): Promise<void> {
    const decision = await this.dialogs.confirm({
      title: t('ledger.detail.voidTitle', { amount: formatMoney(p.amount, p.currency) }),
      message: t('ledger.detail.voidMessage'),
      confirmLabel: t('ledger.detail.voidConfirm'),
      destructive: true,
      reason: { label: t('ledger.detail.voidReasonLabel'), required: true },
    });
    if (!decision) return;
    if (await this.run(() => this.api.voidPayment(r.id, p.id, decision.reason))) {
      this.toasts.show(t('ledger.detail.voidDone'));
    }
  }

  protected openSchedule(r: LedgerReservationDto): void {
    this.scheduleRows.set(toRows(r.installments));
    this.scheduleReason = '';
    this.scheduleError.set(null);
    this.scheduleOpen.set(true);
  }

  protected scheduleSavable(): boolean {
    return scheduleValid(this.scheduleRows()) && (!this.hasPayments() || !!this.scheduleReason.trim());
  }

  protected async saveSchedule(r: LedgerReservationDto): Promise<void> {
    const saved = await this.run(
      () => this.api.replaceSchedule(r.id, r.version, toInstallments(this.scheduleRows()), this.scheduleReason.trim() || null),
      this.scheduleError);
    if (!saved) return;
    this.scheduleOpen.set(false);
    this.toasts.show(t('ledger.scheduleSheet.saved'));
  }

  protected async changeStatus(r: LedgerReservationDto, action: StatusAction): Promise<void> {
    let note: string | null = null;
    if (action.status === 'Cancelled') {
      const decision = await this.dialogs.confirm({
        title: t('ledger.detail.cancelTitle', { reference: r.reference }),
        message: t('ledger.detail.cancelMessage'),
        confirmLabel: t('ledger.detail.cancelConfirm'),
        cancelLabel: t('ledger.detail.cancelKeep'),
        destructive: true,
        reason: { label: t('ledger.detail.cancelNote'), required: true },
        facts: [
          { label: t('ledger.detail.paid'), value: formatMoney(r.paidTotal, r.currency) },
          { label: t('ledger.detail.outstanding'), value: formatMoney(r.outstanding, r.currency) },
        ],
      });
      if (!decision) return;
      note = decision.reason;
    }
    if (await this.run(() => this.api.changeStatus(r.id, r.version, action.status, note))) {
      this.toasts.show(t('ledger.detail.statusChanged'));
    }
  }

  protected async saveReceived(r: LedgerReservationDto): Promise<void> {
    const amount = this.receivedDraft ?? r.commission?.receivedAmount ?? 0;
    if (await this.run(() => this.api.setCommissionReceived(r.id, r.version, amount))) {
      this.receivedDraft = null;
      this.toasts.show(t('ledger.detail.receivedSaved'));
    }
  }

  /** Runs one change; the API answers with the whole reservation, which replaces what is shown. */
  private async run(
    change: () => Promise<LedgerReservationDto>, errorTarget = this.error,
  ): Promise<LedgerReservationDto | null> {
    this.busy.set(true);
    this.error.set(null);
    errorTarget.set(null);
    try {
      const saved = await change();
      this.loaded.set(saved);
      this.history.reload();
      return saved;
    } catch (err) {
      if (problemCode(err) === 'stale_version') {
        this.loaded.reload();
        this.history.reload();
        errorTarget.set(t('ledger.detail.stale'));
      } else {
        errorTarget.set(localizedProblem(err));
      }
      return null;
    } finally {
      this.busy.set(false);
    }
  }

  private newPayment(amount: number) {
    return {
      amount: amount > 0 ? amount : null as number | null,
      paidOn: isoToday(),
      method: 'BankTransfer' as LedgerPaymentMethod,
      reference: '',
      note: '',
      // One key per opened form: a double click or a retried request records the payment once.
      key: crypto.randomUUID(),
    };
  }
}
