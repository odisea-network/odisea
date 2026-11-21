import { Component, inject, input, output, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LedgerDocumentDto, LedgerDocumentType } from '../../../core/api.types';
import { t } from '../../../core/i18n/i18n';
import { localizedProblem } from '../../../core/problem';
import { OdDateTimePipe } from '../../../shared/pipes/date.pipe';
import { TPipe } from '../../../shared/pipes/t.pipe';
import { Banner, ButtonDirective, Card, DialogService, Icon, InputDirective, Skeleton, ToastService } from '../../../shared/ui';
import { LedgerApi } from './ledger-api';

const TYPES: LedgerDocumentType[] = ['OperatorContract', 'Confirmation', 'Invoice', 'PaymentProof', 'Other'];
const MAX_BYTES = 10 * 1024 * 1024;

export function fileSize(bytes: number): string {
  return bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
}

/** Files kept with one ledger reservation. The API decides the real file type from the bytes. */
@Component({
  selector: 'app-ledger-documents',
  imports: [FormsModule, OdDateTimePipe, TPipe, Banner, ButtonDirective, Card, Icon, InputDirective, Skeleton],
  templateUrl: './documents.html',
  styleUrl: './documents.scss',
})
export class LedgerDocuments {
  private readonly api = inject(LedgerApi);
  private readonly dialogs = inject(DialogService);
  private readonly toasts = inject(ToastService);

  readonly reservationId = input.required<string>();
  readonly isAdmin = input.required<boolean>();
  /** An upload or a delete happened; the page refreshes its history. */
  readonly changed = output<void>();

  protected readonly types = TYPES;
  protected type: LedgerDocumentType = 'Confirmation';
  protected readonly busy = signal(false);
  protected readonly working = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly size = fileSize;

  protected readonly documents = resource({
    params: () => this.reservationId(),
    loader: ({ params }) => this.api.documents(params),
  });

  protected typeText(type: LedgerDocumentType): string {
    return t(`ledger.docType.${type}`);
  }

  protected async upload(event: Event): Promise<void> {
    const inputEl = event.target as HTMLInputElement;
    const file = inputEl.files?.[0];
    inputEl.value = '';
    if (!file) return;
    if (file.size > MAX_BYTES) {
      this.error.set(t('ledger.docs.tooLarge'));
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    try {
      const saved = await this.api.uploadDocument(this.reservationId(), file, this.type);
      this.toasts.show(t('ledger.docs.uploaded', { name: saved.fileName }));
      this.documents.reload();
      this.changed.emit();
    } catch (err) {
      this.error.set(localizedProblem(err));
    } finally {
      this.busy.set(false);
    }
  }

  protected async download(doc: LedgerDocumentDto): Promise<void> {
    this.working.set(doc.id);
    try {
      const blob = await this.api.documentFile(this.reservationId(), doc.id);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = doc.fileName;
      anchor.click();
      // Some browsers start programmatic downloads asynchronously; revoking at once can abort them.
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch {
      this.toasts.show(t('ledger.docs.downloadFailed', { name: doc.fileName }), { tone: 'bad' });
    } finally {
      this.working.set(null);
    }
  }

  protected async remove(doc: LedgerDocumentDto): Promise<void> {
    const decision = await this.dialogs.confirm({
      title: t('ledger.docs.deleteTitle', { name: doc.fileName }),
      message: t('ledger.docs.deleteMessage'),
      confirmLabel: t('ledger.docs.deleteConfirm'),
      destructive: true,
    });
    if (!decision) return;
    this.working.set(doc.id);
    this.error.set(null);
    try {
      await this.api.deleteDocument(this.reservationId(), doc.id);
      this.toasts.show(t('ledger.docs.deleted'));
      this.documents.reload();
      this.changed.emit();
    } catch (err) {
      this.error.set(localizedProblem(err));
    } finally {
      this.working.set(null);
    }
  }
}
