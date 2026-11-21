import { HttpClient } from '@angular/common/http';
import { Component, inject, resource, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { FinancialDocumentDto } from '../../core/api.types';
import { t, tOptional } from '../../core/i18n/i18n';
import { OdDatePipe } from '../../shared/pipes/date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { TPipe } from '../../shared/pipes/t.pipe';
import {
  ButtonDirective, Card, EmptyState, Icon, PageHeader, Pill, Skeleton, ToastService,
} from '../../shared/ui';

@Component({
  selector: 'app-agency-documents',
  imports: [MoneyPipe, OdDatePipe, TPipe, ButtonDirective, Card, EmptyState, Icon, PageHeader, Pill, Skeleton],
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
    return tOptional(`agencyDocuments.kind.${kind}`) ?? kind;
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
      this.toasts.show(t('agencyDocuments.downloadFailed', { number: doc.number }), { tone: 'bad' });
    } finally {
      this.downloading.set(null);
    }
  }
}
