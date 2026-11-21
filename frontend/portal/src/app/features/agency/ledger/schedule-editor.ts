import { Component, computed, input, model, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LedgerInstallmentLabel } from '../../../core/api.types';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';
import { t } from '../../../core/i18n/i18n';
import { TPipe } from '../../../shared/pipes/t.pipe';
import { Banner, ButtonDirective, Icon, InputDirective } from '../../../shared/ui';
import { MAX_INSTALLMENTS, ScheduleRow, scheduleDifference, scheduleTotal, scheduleValid, splitDeposit } from './ledger.model';

const LABELS: LedgerInstallmentLabel[] = ['Deposit', 'Balance', 'Other'];

/**
 * The instalments owed to the tour operator: any number of rows, each with an amount and a due
 * date. A deposit percentage only fills in amounts. The total may differ from the price; the
 * difference is shown, never blocked, because operators often confirm the dates later.
 */
@Component({
  selector: 'app-ledger-schedule-editor',
  imports: [FormsModule, MoneyPipe, TPipe, Banner, ButtonDirective, Icon, InputDirective],
  templateUrl: './schedule-editor.html',
  styleUrl: './schedule-editor.scss',
})
export class LedgerScheduleEditor {
  readonly rows = model.required<ScheduleRow[]>();
  readonly operatorTotal = input.required<number | null>();
  readonly currency = input.required<string>();
  readonly disabled = input(false);

  protected readonly labels = LABELS;
  protected readonly depositPercent = signal<number | null>(30);
  protected readonly depositDue = signal('');
  protected readonly balanceDue = signal('');

  protected readonly total = computed(() => scheduleTotal(this.rows()));
  protected readonly difference = computed(() => scheduleDifference(this.operatorTotal(), this.rows()));
  protected readonly valid = computed(() => scheduleValid(this.rows()));
  protected readonly canAdd = computed(() => !this.disabled() && this.rows().length < MAX_INSTALLMENTS);
  protected readonly canSplit = computed(() => {
    const percent = this.depositPercent();
    return !this.disabled() && (this.operatorTotal() ?? 0) > 0 && percent !== null && percent >= 0 && percent <= 100
      && !!this.depositDue() && !!this.balanceDue();
  });

  protected labelText(label: LedgerInstallmentLabel): string {
    return t(`ledger.installmentLabel.${label}`);
  }

  protected add(): void {
    const rest = this.difference();
    this.rows.update(rows => [
      ...rows,
      { label: rows.length === 0 ? 'Deposit' : 'Other', amount: rest > 0 ? rest : null, dueDate: '' },
    ]);
  }

  protected remove(index: number): void {
    this.rows.update(rows => rows.filter((_, i) => i !== index));
  }

  protected patch(index: number, change: Partial<ScheduleRow>): void {
    this.rows.update(rows => rows.map((row, i) => (i === index ? { ...row, ...change } : row)));
  }

  protected split(): void {
    this.rows.set(splitDeposit(this.operatorTotal() ?? 0, this.depositPercent() ?? 0, this.depositDue(), this.balanceDue()));
  }
}
