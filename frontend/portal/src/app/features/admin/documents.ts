import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, computed, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import {
  AgencyDto, IssueInvoiceRequest, OperatorBookingSummary, OperatorDocumentDto, PagedResult,
} from '../../core/api.types';
import { problemMessage } from '../../core/problem';
import { OdDatePipe } from '../../shared/pipes/date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import {
  Banner, ButtonDirective, Card, DialogService, EmptyState, Field, Icon, InputDirective, PageHeader, Pill,
  Sheet, Skeleton, ToastService,
} from '../../shared/ui';
import { VAT_TREATMENT_LABELS, documentKindLabel, vatTreatmentShort } from './document-labels';

/** Фактури and известия: issued in a gapless series, never deleted, annulled with a reason. */
@Component({
  selector: 'app-admin-documents',
  imports: [
    FormsModule, MoneyPipe, OdDatePipe,
    Banner, ButtonDirective, Card, EmptyState, Field, Icon, InputDirective, PageHeader, Pill, Sheet, Skeleton,
  ],
  templateUrl: './documents.html',
  styleUrl: './documents.scss',
})
export class AdminDocuments {
  private readonly http = inject(HttpClient);
  private readonly dialogs = inject(DialogService);
  private readonly toasts = inject(ToastService);

  readonly kind = signal('');
  readonly agencyId = signal('');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly downloading = signal<string | null>(null);
  readonly issueOpen = signal(false);
  readonly creditFor = signal<OperatorDocumentDto | null>(null);

  readonly documents = resource({
    params: () => ({ kind: this.kind(), agencyId: this.agencyId() }),
    loader: ({ params }) => {
      let query = new HttpParams();
      if (params.kind) query = query.set('kind', params.kind);
      if (params.agencyId) query = query.set('agencyId', params.agencyId);
      return firstValueFrom(this.http.get<OperatorDocumentDto[]>('/api/v1/documents', { params: query }));
    },
  });
  readonly agencies = resource({
    loader: () => firstValueFrom(this.http.get<AgencyDto[]>('/api/v1/agencies')),
  });
  /** Confirmed bookings are the only ones an invoice can be issued for. */
  readonly confirmed = resource({
    params: () => (this.issueOpen() ? { open: true } : undefined),
    loader: () => firstValueFrom(this.http.get<PagedResult<OperatorBookingSummary>>(
      '/api/v1/admin/bookings', { params: new HttpParams().set('status', 'Confirmed').set('pageSize', 200) })),
  });

  readonly agencyNames = computed(() => new Map((this.agencies.value() ?? []).map(a => [a.id, a.name])));
  readonly vatOptions = Object.entries(VAT_TREATMENT_LABELS).map(([value, label]) => ({ value, label }));

  readonly kindLabel = documentKindLabel;
  readonly vatLabel = vatTreatmentShort;

  issue: IssueInvoiceRequest = { bookingId: '', vatTreatment: 'MarginScheme', exemptionGrounds: '', dueInDays: 14 };
  creditReason = '';

  agencyName(id: string): string {
    return this.agencyNames().get(id) ?? '';
  }

  openIssue(): void {
    this.issue = { bookingId: '', vatTreatment: 'MarginScheme', exemptionGrounds: '', dueInDays: 14 };
    this.error.set(null);
    this.issueOpen.set(true);
  }

  issueValid(): boolean {
    const i = this.issue;
    return !!i.bookingId && i.dueInDays >= 0 && i.dueInDays <= 60
      && (i.vatTreatment !== 'Exempt' || !!i.exemptionGrounds?.trim());
  }

  async issueInvoice(): Promise<void> {
    if (!this.issueValid()) return;
    await this.act(() => firstValueFrom(this.http.post<OperatorDocumentDto>('/api/v1/documents/invoices', {
      bookingId: this.issue.bookingId, vatTreatment: this.issue.vatTreatment,
      exemptionGrounds: this.issue.exemptionGrounds?.trim() || null, dueInDays: Number(this.issue.dueInDays),
    })), doc => `Invoice ${doc.number} issued`, () => this.issueOpen.set(false));
  }

  openCredit(doc: OperatorDocumentDto): void {
    this.creditReason = '';
    this.error.set(null);
    this.creditFor.set(doc);
  }

  closeCredit(open: boolean): void {
    if (!open) this.creditFor.set(null);
  }

  async issueCreditNote(): Promise<void> {
    const invoice = this.creditFor();
    if (!invoice || !this.creditReason.trim()) return;
    await this.act(() => firstValueFrom(this.http.post<OperatorDocumentDto>('/api/v1/documents/credit-notes', {
      invoiceId: invoice.id, reason: this.creditReason.trim(),
    })), doc => `Credit note ${doc.number} issued`, () => this.creditFor.set(null));
  }

  async annul(doc: OperatorDocumentDto): Promise<void> {
    const decision = await this.dialogs.confirm({
      title: `Annul ${doc.number}?`,
      message: 'The number stays in the series and the document is kept, marked as annulled with your reason. This can’t be undone.',
      confirmLabel: 'Annul document',
      destructive: true,
      reason: { label: 'Reason', placeholder: 'e.g. wrong recipient', required: true },
    });
    if (!decision) return;
    await this.act(() => firstValueFrom(this.http.post<OperatorDocumentDto>(`/api/v1/documents/${doc.id}/annul`, { reason: decision.reason })),
      () => `${doc.number} annulled`);
  }

  async download(doc: OperatorDocumentDto): Promise<void> {
    this.downloading.set(doc.id);
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
    } finally {
      this.downloading.set(null);
    }
  }

  private async act(request: () => Promise<OperatorDocumentDto>, done: (doc: OperatorDocumentDto) => string, after?: () => void): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      const doc = await request();
      this.documents.reload();
      this.toasts.show(done(doc));
      after?.();
    } catch (err) {
      this.error.set(problemMessage(err, 'The document could not be issued.'));
    } finally {
      this.busy.set(false);
    }
  }
}
