import { Component, computed, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { LedgerReservationStatus, LedgerReservationSummaryDto } from '../../../core/api.types';
import { t } from '../../../core/i18n/i18n';
import { ViewportService } from '../../../core/viewport';
import { OdDatePipe } from '../../../shared/pipes/date.pipe';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';
import { TPipe } from '../../../shared/pipes/t.pipe';
import {
  ButtonDirective, Card, EmptyState, Icon, InputDirective, PageHeader, Pager, Pill, SegmentOption, Segmented, Skeleton,
} from '../../../shared/ui';
import { LedgerApi } from './ledger-api';
import { STATUSES, statusTone } from './ledger.model';

export type DueView = 'all' | 'overdue' | 'week' | 'month';

/** A due view is a server-side filter: which instalments are still open is only known there. */
export function dueFilter(view: DueView): { dueWithinDays: number | null; overdueOnly: boolean } {
  switch (view) {
    case 'overdue': return { dueWithinDays: null, overdueOnly: true };
    case 'week': return { dueWithinDays: 7, overdueOnly: false };
    case 'month': return { dueWithinDays: 30, overdueOnly: false };
    default: return { dueWithinDays: null, overdueOnly: false };
  }
}

const PAGE_SIZE = 25;

@Component({
  selector: 'app-ledger-reservations',
  imports: [
    FormsModule, RouterLink, MoneyPipe, OdDatePipe, TPipe,
    ButtonDirective, Card, EmptyState, Icon, InputDirective, PageHeader, Pager, Pill, Segmented, Skeleton,
  ],
  templateUrl: './reservations.html',
  styleUrl: './reservations.scss',
})
export class LedgerReservations {
  private readonly api = inject(LedgerApi);
  private readonly router = inject(Router);

  protected readonly phone = inject(ViewportService).isPhone;
  protected readonly statuses = STATUSES;
  protected readonly pageSize = PAGE_SIZE;

  /** Typed text; applied to the query on submit so every keystroke is not a request. */
  protected draft = '';
  protected readonly q = signal('');
  protected readonly status = signal<LedgerReservationStatus | ''>('');
  protected readonly operatorId = signal('');
  protected readonly view = signal<DueView>('all');
  protected readonly page = signal(1);

  protected readonly views: SegmentOption[] = [
    { value: 'all', label: t('ledger.list.viewAll') },
    { value: 'overdue', label: t('ledger.list.viewOverdue') },
    { value: 'week', label: t('ledger.list.viewWeek') },
    { value: 'month', label: t('ledger.list.viewMonth') },
  ];

  protected readonly operators = resource({ loader: () => this.api.operators(true) });

  protected readonly reservations = resource({
    params: () => ({
      q: this.q(), status: this.status(), operatorId: this.operatorId(), view: this.view(), page: this.page(),
    }),
    loader: ({ params }) => this.api.list({
      q: params.q, status: params.status, operatorId: params.operatorId, ...dueFilter(params.view),
      page: params.page, pageSize: PAGE_SIZE,
    }),
  });

  protected readonly hasFilters = computed(() =>
    this.q() !== '' || this.status() !== '' || this.operatorId() !== '' || this.view() !== 'all');

  protected statusLabel(status: LedgerReservationStatus): string {
    return t(`ledger.status.${status}`);
  }

  protected readonly tone = statusTone;

  protected search(): void {
    this.q.set(this.draft.trim());
    this.page.set(1);
  }

  protected setStatus(status: string): void {
    this.status.set(status as LedgerReservationStatus | '');
    this.page.set(1);
  }

  protected setOperator(id: string): void {
    this.operatorId.set(id);
    this.page.set(1);
  }

  protected setView(view: string): void {
    this.view.set(view as DueView);
    this.page.set(1);
  }

  protected clear(): void {
    this.draft = '';
    this.q.set('');
    this.status.set('');
    this.operatorId.set('');
    this.view.set('all');
    this.page.set(1);
  }

  protected open(row: LedgerReservationSummaryDto): void {
    void this.router.navigate(['/agency/ledger/reservations', row.id]);
  }
}
