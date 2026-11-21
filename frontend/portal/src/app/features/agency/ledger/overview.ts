import { Component, inject, resource } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LedgerCurrencyTotalsDto, LedgerDueItemDto } from '../../../core/api.types';
import { formatMoney } from '../../../core/format';
import { t } from '../../../core/i18n/i18n';
import { OdDatePipe } from '../../../shared/pipes/date.pipe';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';
import { TPipe } from '../../../shared/pipes/t.pipe';
import { ButtonDirective, Card, EmptyState, Icon, Kpi, PageHeader, Pill, Skeleton } from '../../../shared/ui';
import { LedgerApi } from './ledger-api';

/** One figure per currency, side by side: amounts in different currencies are never added up. */
export function perCurrency(totals: LedgerCurrencyTotalsDto[], pick: (row: LedgerCurrencyTotalsDto) => number): string {
  return totals.filter(row => pick(row) > 0).map(row => formatMoney(pick(row), row.currency)).join(' · ');
}

/** What the agency owes and when, at a glance; every figure links to the list behind it. */
@Component({
  selector: 'app-ledger-overview',
  imports: [RouterLink, MoneyPipe, OdDatePipe, TPipe, ButtonDirective, Card, EmptyState, Icon, Kpi, PageHeader, Pill, Skeleton],
  templateUrl: './overview.html',
  styleUrl: './overview.scss',
})
export class LedgerOverview {
  private readonly api = inject(LedgerApi);

  protected readonly list = '/agency/ledger/reservations';
  protected readonly dashboard = resource({ loader: () => this.api.dashboard() });

  protected foot(totals: LedgerCurrencyTotalsDto[], pick: (row: LedgerCurrencyTotalsDto) => number, empty: string): string {
    return perCurrency(totals, pick) || empty;
  }

  protected readonly overdueOf = (row: LedgerCurrencyTotalsDto) => row.overdue;
  protected readonly weekOf = (row: LedgerCurrencyTotalsDto) => row.dueIn7Days;
  protected readonly monthOf = (row: LedgerCurrencyTotalsDto) => row.dueIn30Days;

  protected readonly overdueNone = t('ledger.dashboard.overdueNone');
  protected readonly dueFoot = t('ledger.dashboard.dueFoot');

  protected labelText(item: LedgerDueItemDto): string {
    return t(`ledger.installmentLabel.${item.label}`);
  }
}
