import { describe, expect, it } from 'vitest';
import { operatorActions, parseBreakdown } from './booking-actions';

const now = new Date('2026-10-24T12:00:00Z');
const awaiting = { status: 'AwaitingPayment', sellAmount: 1000, balanceDue: 1000, paymentDueAt: '2026-10-27T12:00:00Z' };

describe('operatorActions', () => {
  it('lets any operator record payments and reject while awaiting payment, but not confirm', () => {
    const actions = operatorActions(awaiting, false, now);
    expect(actions.recordPayment).toBe(true);
    expect(actions.reject).toBe(true);
    expect(actions.confirm).toBe(false);
    expect(actions.cancel).toBe(false);
    expect(actions.voidPayment).toBe(false);
    expect(actions.expire).toBe(false);
  });

  it('opens confirmation to admins only once the balance is covered', () => {
    expect(operatorActions(awaiting, true, now).confirm).toBe(false);
    expect(operatorActions({ ...awaiting, balanceDue: 0 }, true, now).confirm).toBe(true);
    expect(operatorActions({ ...awaiting, balanceDue: 0 }, false, now).confirm).toBe(false);
  });

  it('offers expiry only once the payment deadline has passed', () => {
    expect(operatorActions(awaiting, false, new Date('2026-10-27T12:00:01Z')).expire).toBe(true);
    expect(operatorActions({ ...awaiting, paymentDueAt: null }, false, now).expire).toBe(false);
  });

  it('reserves cancellation and voiding for admins, cancel also from Confirmed and the rescue state', () => {
    expect(operatorActions({ ...awaiting, status: 'Confirmed', balanceDue: 0 }, true, now).cancel).toBe(true);
    expect(operatorActions({ ...awaiting, status: 'PendingConfirmation' }, true, now).cancel).toBe(true);
    expect(operatorActions({ ...awaiting, status: 'Confirmed', balanceDue: 0 }, false, now).cancel).toBe(false);
    expect(operatorActions(awaiting, true, now).voidPayment).toBe(true);
    for (const status of ['Draft', 'PriceConfirmed', 'Rejected', 'Expired', 'Cancelled', 'Failed']) {
      const actions = operatorActions({ ...awaiting, status }, true, now);
      expect(Object.values(actions).some(Boolean)).toBe(false);
    }
  });
});

describe('parseBreakdown', () => {
  it('reads the stored PascalCase pricing breakdown', () => {
    const parsed = parseBreakdown({
      NetCost: { Amount: 892.5, Currency: 'EUR' },
      Steps: [{ RuleId: 'r', Description: 'Global markup 15%', Kind: 'Markup',
        Before: { Amount: 892.5, Currency: 'EUR' }, After: { Amount: 1026.38, Currency: 'EUR' } }],
      SellPrice: { Amount: 1036.38, Currency: 'EUR' },
      AgencyCommission: { Amount: 103.64, Currency: 'EUR' },
    });
    expect(parsed).not.toBeNull();
    expect(parsed!.netCost).toBe(892.5);
    expect(parsed!.sellPrice).toBe(1036.38);
    expect(parsed!.agencyCommission).toBe(103.64);
    expect(parsed!.steps).toEqual([{ description: 'Global markup 15%', kind: 'Markup', before: 892.5, after: 1026.38 }]);
  });

  it('returns null for missing or malformed data instead of throwing', () => {
    expect(parseBreakdown(null)).toBeNull();
    expect(parseBreakdown('text')).toBeNull();
    expect(parseBreakdown({ Steps: [] })).toBeNull();
  });
});
