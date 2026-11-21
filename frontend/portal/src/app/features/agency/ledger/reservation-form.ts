import { Component, computed, effect, inject, input, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import {
  CommissionBase, CommissionRequest, CommissionType, LedgerReservationDto, LedgerReservationStatus,
  SaveLedgerReservationRequest,
} from '../../../core/api.types';
import { AuthService } from '../../../core/auth.service';
import { t } from '../../../core/i18n/i18n';
import { localizedProblem, problemCode } from '../../../core/problem';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';
import { TPipe } from '../../../shared/pipes/t.pipe';
import {
  Banner, ButtonDirective, Card, Crumb, EmptyState, Field, Icon, InputDirective, PageHeader, Sheet, Skeleton,
  ToastService,
} from '../../../shared/ui';
import { LedgerApi } from './ledger-api';
import { CURRENCIES, ScheduleRow, isoToday, previewCommission, scheduleValid, toInstallments } from './ledger.model';
import { LedgerScheduleEditor } from './schedule-editor';

interface FormModel {
  operatorId: string;
  reference: string;
  holderName: string;
  bookedOn: string;
  travelStart: string;
  travelEnd: string;
  operatorTotal: number | null;
  currency: string;
  customerSale: number | null;
  status: LedgerReservationStatus;
  notes: string;
  commissionType: CommissionType | '';
  commissionRate: number | null;
  commissionBase: CommissionBase;
  commissionAmount: number | null;
  reason: string;
}

function emptyForm(): FormModel {
  return {
    operatorId: '', reference: '', holderName: '', bookedOn: isoToday(), travelStart: '', travelEnd: '',
    operatorTotal: null, currency: 'EUR', customerSale: null, status: 'Confirmed', notes: '',
    commissionType: '', commissionRate: null, commissionBase: 'OperatorTotal', commissionAmount: null, reason: '',
  };
}

export function formOf(r: LedgerReservationDto): FormModel {
  const c = r.commission;
  const manual = c !== null && (c.type === 'Fixed' || c.base === 'Manual');
  return {
    operatorId: r.tourOperatorId, reference: r.reference, holderName: r.holderName, bookedOn: r.bookedOn,
    travelStart: r.travelStart, travelEnd: r.travelEnd ?? '', operatorTotal: r.operatorTotal, currency: r.currency,
    customerSale: r.customerSaleAmount, status: r.status, notes: r.notes ?? '',
    // A percentage with a typed amount is stored as a manual amount; the form shows it as a fixed sum.
    commissionType: c === null ? '' : manual ? 'Fixed' : 'Percent',
    commissionRate: c?.rate ?? null,
    commissionBase: c !== null && c.base === 'CustomerSale' ? 'CustomerSale' : 'OperatorTotal',
    commissionAmount: manual ? c.expectedAmount : null,
    reason: '',
  };
}

export function commissionOf(form: FormModel): CommissionRequest | null {
  if (form.commissionType === '') return null;
  return form.commissionType === 'Fixed'
    ? { type: 'Fixed', rate: null, base: 'Manual', amount: form.commissionAmount }
    : { type: 'Percent', rate: form.commissionRate, base: form.commissionBase, amount: null };
}

/** Create or edit one ledger reservation. The schedule is entered here on create and edited on the reservation page later. */
@Component({
  selector: 'app-ledger-reservation-form',
  imports: [
    FormsModule, RouterLink, MoneyPipe, TPipe,
    Banner, ButtonDirective, Card, EmptyState, Field, Icon, InputDirective, PageHeader, Sheet, Skeleton,
    LedgerScheduleEditor,
  ],
  templateUrl: './reservation-form.html',
  styleUrl: './reservation-form.scss',
})
export class LedgerReservationForm {
  private readonly api = inject(LedgerApi);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);
  private readonly auth = inject(AuthService);

  /** Route parameter; absent on `/new`. */
  readonly id = input<string>();

  protected readonly currencies = CURRENCIES;
  protected readonly today = isoToday();
  protected readonly isAdmin = computed(() => this.auth.user()?.role === 'AgencyAdmin');

  protected form = emptyForm();
  protected readonly rows = signal<ScheduleRow[]>([]);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly operators = resource({ loader: () => this.api.operators(false) });
  protected readonly existing = resource({
    params: () => this.id(),
    loader: ({ params }) => this.api.get(params),
  });

  protected readonly operatorSheet = signal(false);
  protected operatorName = '';
  protected readonly operatorError = signal<string | null>(null);

  protected readonly crumbs = computed<Crumb[]>(() => {
    const list: Crumb[] = [{ label: t('ledger.form.crumb'), link: '/agency/ledger/reservations' }];
    const existing = this.existing.value();
    if (existing) list.push({ label: existing.reference, link: ['/agency/ledger/reservations', existing.id] });
    return list;
  });

  constructor() {
    effect(() => {
      const existing = this.existing.value();
      if (existing) this.form = formOf(existing);
    });
  }

  protected get hasPayments(): boolean {
    return (this.existing.value()?.payments ?? []).some(p => !p.isVoided);
  }

  /** After a payment the price is a correction to recorded money: admin only, with a reason. */
  protected get priceLocked(): boolean {
    return this.hasPayments && !this.isAdmin();
  }

  protected get needsReason(): boolean {
    const existing = this.existing.value();
    return !!existing && this.hasPayments && this.form.operatorTotal !== existing.operatorTotal;
  }

  protected get commissionPreview(): number | null {
    const f = this.form;
    return previewCommission(f.commissionType, f.commissionRate, f.commissionBase, f.operatorTotal, f.customerSale, f.commissionAmount);
  }

  protected get valid(): boolean {
    const f = this.form;
    return !!f.operatorId && !!f.reference.trim() && !!f.holderName.trim() && !!f.bookedOn && !!f.travelStart
      && f.operatorTotal !== null && f.operatorTotal >= 0
      && (!this.needsReason || !!f.reason.trim())
      && (this.id() !== undefined || scheduleValid(this.rows()));
  }

  /** An archived operator is not offered for new bookings but must still show on its old ones. */
  protected operatorListed(id: string): boolean {
    return (this.operators.value() ?? []).some(o => o.id === id);
  }

  protected title(): string {
    const existing = this.existing.value();
    return existing ? t('ledger.form.editTitle', { reference: existing.reference }) : t('ledger.form.newTitle');
  }

  protected async addOperator(): Promise<void> {
    this.operatorError.set(null);
    try {
      const created = await this.api.createOperator({
        name: this.operatorName, contactEmail: null, contactPhone: null, notes: null,
      });
      this.operators.reload();
      this.form.operatorId = created.id;
      this.operatorName = '';
      this.operatorSheet.set(false);
    } catch (err) {
      this.operatorError.set(localizedProblem(err));
    }
  }

  protected async submit(): Promise<void> {
    if (!this.valid) {
      this.error.set(t('ledger.form.missing'));
      return;
    }
    const f = this.form;
    const request: SaveLedgerReservationRequest = {
      tourOperatorId: f.operatorId,
      reference: f.reference.trim(),
      holderName: f.holderName.trim(),
      bookedOn: f.bookedOn,
      travelStart: f.travelStart,
      travelEnd: f.travelEnd || null,
      operatorTotal: f.operatorTotal ?? 0,
      currency: f.currency,
      customerSaleAmount: f.customerSale,
      notes: f.notes.trim() || null,
      commission: commissionOf(f),
    };

    this.busy.set(true);
    this.error.set(null);
    try {
      const existing = this.existing.value();
      const saved = existing
        ? await this.api.update(existing.id, { ...request, version: existing.version, reason: f.reason.trim() || null })
        : await this.api.create({ ...request, status: f.status, installments: toInstallments(this.rows()) });
      this.toasts.show(t('ledger.form.saved', { reference: saved.reference }));
      await this.router.navigate(['/agency/ledger/reservations', saved.id]);
    } catch (err) {
      this.error.set(localizedProblem(err));
      // Someone else saved first: take their version so the next attempt is not refused again.
      if (problemCode(err) === 'stale_version') this.existing.reload();
    } finally {
      this.busy.set(false);
    }
  }
}
