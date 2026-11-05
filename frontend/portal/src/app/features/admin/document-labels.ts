/** Bulgarian tax documents: the law names them, so labels keep both languages. */
export const DOCUMENT_KIND_LABELS: Record<string, string> = {
  Invoice: 'Invoice · Фактура',
  CreditNote: 'Credit note · Кредитно известие',
  DebitNote: 'Debit note · Дебитно известие',
  Proforma: 'Proforma · Проформа',
};

export const VAT_TREATMENT_LABELS: Record<string, string> = {
  MarginScheme: 'Margin scheme · art. 136–142 ЗДДС',
  Standard20: 'Standard 20%',
  Accommodation9: 'Accommodation 9%',
  Exempt: 'Exempt · grounds required',
};

const VAT_TREATMENT_SHORT: Record<string, string> = {
  MarginScheme: 'Margin scheme',
  Standard20: 'VAT 20%',
  Accommodation9: 'VAT 9%',
  Exempt: 'Exempt',
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
