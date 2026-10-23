import { HttpClient } from '@angular/common/http';
import { Component, inject, resource, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { FinancialDocumentDto } from '../../core/api.types';
import { formatDate, formatMoney } from '../../core/format';

const KIND_LABELS: Record<string, string> = {
  Invoice: 'Фактура',
  CreditNote: 'Кредитно известие',
  DebitNote: 'Дебитно известие',
  Proforma: 'Проформа',
};

@Component({
  selector: 'app-agency-documents',
  template: `
    <h2>Documents</h2>
    @if (documents.isLoading()) {
      <p>Loading…</p>
    } @else {
      <table class="card">
        <thead>
          <tr>
            <th>№</th><th>Вид</th><th>Дата</th><th>Резервация</th>
            <th class="num">Сума</th><th>Статус</th><th></th>
          </tr>
        </thead>
        <tbody>
          @for (d of documents.value(); track d.id) {
            <tr>
              <td class="strong">{{ d.number }}</td>
              <td>{{ kindLabel(d.kind) }}
                @if (d.relatedDocumentNumber) {
                  <span class="muted">→ № {{ d.relatedDocumentNumber }}</span>
                }
              </td>
              <td>{{ fmtDate(d.issueDate) }}</td>
              <td>{{ d.bookingRef ?? '—' }}</td>
              <td class="num">{{ fmtMoney(d.total, d.currency) }}</td>
              <td>{{ d.status === 'Annulled' ? 'Анулиран' : 'Издаден' }}</td>
              <td>
                <button type="button" class="ghost" (click)="download(d)" [disabled]="downloading() === d.id">
                  {{ downloading() === d.id ? '…' : 'PDF' }}
                </button>
              </td>
            </tr>
          } @empty {
            <tr><td colspan="7">No documents yet.</td></tr>
          }
        </tbody>
      </table>
    }
    @if (error()) {
      <p class="error" role="alert">{{ error() }}</p>
    }
  `,
  styles: `
    .card { background: var(--surface-1); border: 1px solid var(--border); border-radius: .75rem; width: 100%; border-collapse: collapse; }
    th, td { text-align: left; padding: .7rem 1rem; border-bottom: 1px solid var(--border); }
    th.num, td.num { text-align: right; }
    .strong { font-weight: 700; }
    .muted { color: var(--text-muted); font-size: .85rem; }
    .error { color: var(--danger); }
  `,
})
export class AgencyDocuments {
  private readonly http = inject(HttpClient);

  readonly downloading = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  readonly documents = resource({
    loader: () => firstValueFrom(this.http.get<FinancialDocumentDto[]>('/api/v1/agency/documents')),
  });

  readonly fmtMoney = formatMoney;
  readonly fmtDate = formatDate;

  kindLabel(kind: string): string {
    return KIND_LABELS[kind] ?? kind;
  }

  /** Downloaded through HttpClient so the auth interceptor applies; the blob
   *  URL is revoked immediately — nothing lingers in the page. */
  async download(doc: FinancialDocumentDto): Promise<void> {
    this.downloading.set(doc.id);
    this.error.set(null);
    try {
      const blob = await firstValueFrom(
        this.http.get(`/api/v1/agency/documents/${doc.id}/pdf`, { responseType: 'blob' }),
      );
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `${this.kindLabel(doc.kind)}-${doc.number}.pdf`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch {
      this.error.set('Download failed.');
    } finally {
      this.downloading.set(null);
    }
  }
}
