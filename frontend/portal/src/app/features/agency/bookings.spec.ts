import { describe, expect, it } from 'vitest';
import { agencyCanCancel } from './bookings';

describe('agencyCanCancel', () => {
  it('allows withdrawal before any money has been recorded', () => {
    expect(agencyCanCancel({ status: 'Draft', paidAmount: 0 })).toBe(true);
    expect(agencyCanCancel({ status: 'PriceConfirmed', paidAmount: 0 })).toBe(true);
    expect(agencyCanCancel({ status: 'AwaitingPayment', paidAmount: 0 })).toBe(true);
  });

  it('hands over to the operator once paid, confirmed, in flight or finished', () => {
    expect(agencyCanCancel({ status: 'AwaitingPayment', paidAmount: 100 })).toBe(false);
    expect(agencyCanCancel({ status: 'PendingConfirmation', paidAmount: 0 })).toBe(false);
    expect(agencyCanCancel({ status: 'Confirmed', paidAmount: 1000 })).toBe(false);
    for (const status of ['Rejected', 'Expired', 'Cancelled', 'Failed']) {
      expect(agencyCanCancel({ status, paidAmount: 0 })).toBe(false);
    }
  });
});
