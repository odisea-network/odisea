import { describe, expect, it } from 'vitest';
import { BookingDto } from '../../core/api.types';
import { agencyCanCancel, matchesView } from './bookings';

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

describe('matchesView', () => {
  const today = '2026-10-25';
  const booking = (status: string, checkIn = '2026-11-14'): BookingDto =>
    ({ status, checkIn } as unknown as BookingDto);

  it('"needs payment" is exactly the bookings waiting on money', () => {
    expect(matchesView(booking('AwaitingPayment'), 'due', today)).toBe(true);
    expect(matchesView(booking('Confirmed'), 'due', today)).toBe(false);
  });

  it('"upcoming" is confirmed and not yet departed', () => {
    expect(matchesView(booking('Confirmed', '2026-11-14'), 'upcoming', today)).toBe(true);
    expect(matchesView(booking('Confirmed', '2026-10-01'), 'upcoming', today)).toBe(false);
    expect(matchesView(booking('AwaitingPayment', '2026-11-14'), 'upcoming', today)).toBe(false);
  });

  it('drafts and closed cover the rest; all is everything', () => {
    expect(matchesView(booking('Draft'), 'drafts', today)).toBe(true);
    expect(matchesView(booking('PriceConfirmed'), 'drafts', today)).toBe(true);
    for (const status of ['Rejected', 'Expired', 'Cancelled', 'Failed']) {
      expect(matchesView(booking(status), 'closed', today)).toBe(true);
      expect(matchesView(booking(status), 'all', today)).toBe(true);
    }
    expect(matchesView(booking('PendingConfirmation'), 'closed', today)).toBe(false);
  });
});
