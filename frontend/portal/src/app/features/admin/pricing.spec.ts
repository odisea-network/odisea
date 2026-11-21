import { describe, expect, it } from 'vitest';
import { ruleValidityLabel, ruleValueLabel } from './pricing';

describe('ruleValueLabel', () => {
  it('signs markups and fees, in percent or euros', () => {
    expect(ruleValueLabel({ kind: 'Markup', valueType: 'Percent', value: 10 })).toBe('+10%');
    expect(ruleValueLabel({ kind: 'Markup', valueType: 'Percent', value: -5 })).toBe('−5%');
    expect(ruleValueLabel({ kind: 'Fee', valueType: 'FixedAmount', value: 20 })).toContain('+20,00');
  });

  it('phrases commissions as a share of the sell price', () => {
    expect(ruleValueLabel({ kind: 'Commission', valueType: 'Percent', value: 10 })).toBe('10% от продажната цена');
    expect(ruleValueLabel({ kind: 'Commission', valueType: 'FixedAmount', value: 15 })).toContain('на резервация');
  });
});

describe('ruleValidityLabel', () => {
  const fmt = (d: string) => d;

  it('covers open-ended and closed ranges', () => {
    expect(ruleValidityLabel({ validFrom: null, validTo: null }, fmt)).toBe('Постоянно');
    expect(ruleValidityLabel({ validFrom: '2026-01-01', validTo: null }, fmt)).toBe('От 2026-01-01');
    expect(ruleValidityLabel({ validFrom: null, validTo: '2026-12-31' }, fmt)).toBe('До 2026-12-31');
    expect(ruleValidityLabel({ validFrom: '2026-01-01', validTo: '2026-12-31' }, fmt)).toBe('2026-01-01 – 2026-12-31');
  });
});
