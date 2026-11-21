import { t } from '../../core/i18n/i18n';

/** Bulgarian tax documents, named as the law names them. */
export const DOCUMENT_KIND_LABELS: Record<string, string> = {
  Invoice: t('agencyDocuments.kind.Invoice'),
  CreditNote: t('agencyDocuments.kind.CreditNote'),
  DebitNote: t('agencyDocuments.kind.DebitNote'),
  Proforma: t('agencyDocuments.kind.Proforma'),
};

export const VAT_TREATMENT_LABELS: Record<string, string> = {
  MarginScheme: t('adminDocuments.vat.MarginScheme'),
  Standard20: t('adminDocuments.vat.Standard20'),
  Accommodation9: t('adminDocuments.vat.Accommodation9'),
  Exempt: t('adminDocuments.vat.Exempt'),
};

const VAT_TREATMENT_SHORT: Record<string, string> = {
  MarginScheme: t('adminDocuments.vatShort.MarginScheme'),
  Standard20: t('adminDocuments.vatShort.Standard20'),
  Accommodation9: t('adminDocuments.vatShort.Accommodation9'),
  Exempt: t('adminDocuments.vatShort.Exempt'),
};

/** Table-length form of the treatment. */
export function vatTreatmentShort(treatment: string): string {
  return VAT_TREATMENT_SHORT[treatment] ?? treatment;
}

export function documentKindLabel(kind: string): string {
  return DOCUMENT_KIND_LABELS[kind] ?? kind;
}

export function vatTreatmentLabel(treatment: string): string {
  return VAT_TREATMENT_LABELS[treatment] ?? treatment;
}
