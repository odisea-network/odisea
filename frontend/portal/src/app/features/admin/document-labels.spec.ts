import { describe, expect, it } from 'vitest';
import { documentKindLabel, vatTreatmentLabel, vatTreatmentShort } from './document-labels';

describe('document labels', () => {
  it('names the kinds in both languages and falls back to the raw value', () => {
    expect(documentKindLabel('Invoice')).toContain('Фактура');
    expect(documentKindLabel('CreditNote')).toContain('Кредитно');
    expect(documentKindLabel('Voucher')).toBe('Voucher');
  });

  it('explains the VAT treatments', () => {
    expect(vatTreatmentLabel('MarginScheme')).toContain('136');
    expect(vatTreatmentLabel('Exempt')).toContain('grounds');
    expect(vatTreatmentLabel('Other')).toBe('Other');
    expect(vatTreatmentShort('Standard20')).toBe('VAT 20%');
    expect(vatTreatmentShort('MarginScheme')).toBe('Margin scheme');
  });
});
