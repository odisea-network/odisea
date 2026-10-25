import { HttpClient } from '@angular/common/http';
import { Component, inject, resource, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { FinancialDocumentDto } from '../../core/api.types';
import { OdDatePipe } from '../../shared/pipes/date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import {
  ButtonDirective, Card, EmptyState, Icon, PageHeader, Pill, Skeleton, ToastService,
} from '../../shared/ui';

/** Document kinds are Bulgarian tax documents; the law names them, so the label keeps both. */
const KIND_LABELS: Record<string, string> = {
  Invoice: 'Invoice · Фактура',
  CreditNote: 'Credit note · Кредитно известие',
  DebitNote: 'Debit note · Дебитно известие',
  Proforma: 'Proforma · Проформа',
};

@Component({
  selector: 'app-agency-documents',
  imports: [MoneyPipe, OdDatePipe, ButtonDirective, Card, EmptyState, Icon, PageHeader, Pill, Skeleton],
  templateUrl: './documents.html',
  styleUrl: './documents.scss',
})
export class AgencyDocuments {
  private readonly http = inject(HttpClient);
  private readonly toasts = inject(ToastService);

  readonly downloading = signal<string | null>(null);

  readonly documents = resource({
    loader: () => firstValueFrom(this.http.get<FinancialDocumentDto[]>('/api/v1/agency/documents')),
  });

  kindLabel(kind: string): string {
    return KIND_LABELS[kind] ?? kind;
  }

  /** Downloaded through HttpClient so the auth interceptor applies. */
  async download(doc: FinancialDocumentDto): Promise<void> {
    this.downloading.set(doc.id);
    try {
      const blob = await firstValueFrom(
        this.http.get(`/api/v1/agency/documents/${doc.id}/pdf`, { responseType: 'blob' }),
      );
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `${doc.kind}-${doc.number}.pdf`;
      anchor.click();
      // Some browsers resolve programmatic downloads asynchronously —
      // revoking in the same task can abort them. Defer it.
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch {
      this.toasts.show(`Download of ${doc.number} failed`, { tone: 'bad' });
    } finally {
      this.downloading.set(null);
    }
  }
}
