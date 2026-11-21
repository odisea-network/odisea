import { Component, computed, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TourOperatorDto } from '../../../core/api.types';
import { AuthService } from '../../../core/auth.service';
import { t } from '../../../core/i18n/i18n';
import { localizedProblem } from '../../../core/problem';
import { TPipe } from '../../../shared/pipes/t.pipe';
import {
  Banner, ButtonDirective, Card, EmptyState, Field, Icon, InputDirective, PageHeader, Pill, Sheet, Skeleton, ToastService,
} from '../../../shared/ui';
import { LedgerApi } from './ledger-api';

/** The agency's own list of tour operators. Archived ones stay on old reservations but are not offered for new ones. */
@Component({
  selector: 'app-ledger-operators',
  imports: [
    FormsModule, TPipe,
    Banner, ButtonDirective, Card, EmptyState, Field, Icon, InputDirective, PageHeader, Pill, Sheet, Skeleton,
  ],
  templateUrl: './operators.html',
  styleUrl: './operators.scss',
})
export class LedgerOperators {
  private readonly api = inject(LedgerApi);
  private readonly toasts = inject(ToastService);
  private readonly auth = inject(AuthService);

  protected readonly isAdmin = computed(() => this.auth.user()?.role === 'AgencyAdmin');
  protected readonly operators = resource({ loader: () => this.api.operators(true) });

  protected readonly sheetOpen = signal(false);
  protected readonly editing = signal<TourOperatorDto | null>(null);
  protected form = { name: '', contactEmail: '', contactPhone: '', notes: '' };
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly sheetError = signal<string | null>(null);

  protected open(operator: TourOperatorDto | null): void {
    this.editing.set(operator);
    this.form = {
      name: operator?.name ?? '', contactEmail: operator?.contactEmail ?? '',
      contactPhone: operator?.contactPhone ?? '', notes: operator?.notes ?? '',
    };
    this.sheetError.set(null);
    this.sheetOpen.set(true);
  }

  protected async save(): Promise<void> {
    const request = {
      name: this.form.name.trim(),
      contactEmail: this.form.contactEmail.trim() || null,
      contactPhone: this.form.contactPhone.trim() || null,
      notes: this.form.notes.trim() || null,
    };
    this.busy.set(true);
    this.sheetError.set(null);
    try {
      const editing = this.editing();
      await (editing ? this.api.updateOperator(editing.id, request) : this.api.createOperator(request));
      this.sheetOpen.set(false);
      this.toasts.show(t('ledger.operatorSheet.saved'));
      this.operators.reload();
    } catch (err) {
      this.sheetError.set(localizedProblem(err));
    } finally {
      this.busy.set(false);
    }
  }

  protected async setArchived(operator: TourOperatorDto, archived: boolean): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.api.setOperatorArchived(operator.id, archived);
      this.toasts.show(t(archived ? 'ledger.operators.archivedDone' : 'ledger.operators.unarchivedDone', { name: operator.name }));
      this.operators.reload();
    } catch (err) {
      this.error.set(localizedProblem(err));
    } finally {
      this.busy.set(false);
    }
  }
}
