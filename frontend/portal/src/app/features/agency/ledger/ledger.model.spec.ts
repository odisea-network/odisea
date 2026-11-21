import { describe, expect, it } from 'vitest';
import { LedgerAuditEventDto, LedgerReservationDto } from '../../../core/api.types';
import {
  auditTimeline, installmentStatusLabel, installmentTone, isoToday, previewCommission, roundMoney, scheduleDifference,
  scheduleTotal, scheduleValid, splitDeposit, toInstallments,
} from './ledger.model';
import { commissionOf, formOf } from './reservation-form';
import { statusActions, suggestedPayment } from './reservation-detail';
import { fileSize } from './documents';
import { perCurrency } from './overview';
import { dueFilter } from './reservations';

describe('ledger schedule helpers', () => {
  it('splits a price into deposit and balance that add up exactly', () => {
    const rows = splitDeposit(2000, 30, '2026-12-01', '2027-01-15');
    expect(rows).toEqual([
      { label: 'Deposit', amount: 600, dueDate: '2026-12-01' },
      { label: 'Balance', amount: 1400, dueDate: '2027-01-15' },
    ]);
    // A third of an odd price: the cents land on the balance, nothing is lost.
    const odd = splitDeposit(1000.01, 33.33, '2026-12-01', '2027-01-15');
    expect(odd.map(r => r.amount)).toEqual([333.3, 666.71]);
    expect(scheduleDifference(1000.01, odd)).toBe(0);
  });

  it('makes a single instalment for a 0 % or 100 % deposit', () => {
    expect(splitDeposit(500, 0, 'a', 'b')).toEqual([{ label: 'Balance', amount: 500, dueDate: 'b' }]);
    expect(splitDeposit(500, 100, 'a', 'b')).toEqual([{ label: 'Deposit', amount: 500, dueDate: 'a' }]);
  });

  it('reports what the schedule leaves out or plans too much', () => {
    const rows = [{ label: 'Other' as const, amount: 750, dueDate: '2026-12-01' }];
    expect(scheduleTotal(rows)).toBe(750);
    expect(scheduleDifference(1000, rows)).toBe(250);
    expect(scheduleDifference(700, rows)).toBe(-50);
    expect(scheduleDifference(null, [])).toBe(0);
  });

  it('adds cents without floating-point dust', () => {
    const rows = [0.1, 0.2, 0.3].map(amount => ({ label: 'Other' as const, amount, dueDate: '2026-12-01' }));
    expect(scheduleTotal(rows)).toBe(0.6);
  });

  it('accepts an empty schedule and refuses rows without an amount or a date', () => {
    expect(scheduleValid([])).toBe(true);
    expect(scheduleValid([{ label: 'Other', amount: 10, dueDate: '2026-12-01' }])).toBe(true);
    expect(scheduleValid([{ label: 'Other', amount: null, dueDate: '2026-12-01' }])).toBe(false);
    expect(scheduleValid([{ label: 'Other', amount: 0, dueDate: '2026-12-01' }])).toBe(false);
    expect(scheduleValid([{ label: 'Other', amount: 10, dueDate: '' }])).toBe(false);
    expect(toInstallments([{ label: 'Deposit', amount: 10, dueDate: '2026-12-01' }]))
      .toEqual([{ label: 'Deposit', amount: 10, dueDate: '2026-12-01' }]);
  });

  it('rounds half a cent away from zero like the API', () => {
    expect(roundMoney(0.1665)).toBe(0.17);
    expect(roundMoney(1.005)).toBe(1.01);
    expect(roundMoney(-0.125)).toBe(-0.13);
  });

  it('writes today as a local calendar date', () => {
    expect(isoToday(new Date(2026, 0, 5, 23, 30))).toBe('2026-01-05');
  });
});

describe('ledger commission preview', () => {
  it('takes a percentage from the chosen base', () => {
    expect(previewCommission('Percent', 7, 'OperatorTotal', 2000, 2300, null)).toBe(140);
    expect(previewCommission('Percent', 7, 'CustomerSale', 2000, 2300, null)).toBe(161);
    expect(previewCommission('Percent', 0.5, 'OperatorTotal', 33.3, null, null)).toBe(0.17);
  });

  it('is unknown until the base amount and the rate are there', () => {
    expect(previewCommission('Percent', 7, 'CustomerSale', 2000, null, null)).toBeNull();
    expect(previewCommission('Percent', null, 'OperatorTotal', 2000, null, null)).toBeNull();
    expect(previewCommission('', 7, 'OperatorTotal', 2000, null, null)).toBeNull();
  });

  it('is the typed amount when fixed', () => {
    expect(previewCommission('Fixed', null, 'OperatorTotal', 2000, null, 75)).toBe(75);
  });
});

describe('ledger labels', () => {
  it('says overdue in words, not only in colour', () => {
    expect(installmentStatusLabel({ status: 'PartiallyPaid', isOverdue: true })).toBe('Частично платена · просрочена');
    expect(installmentStatusLabel({ status: 'Paid', isOverdue: false })).toBe('Платена');
    expect(installmentTone({ status: 'Unpaid', isOverdue: true })).toBe('bad');
    expect(installmentTone({ status: 'PartiallyPaid', isOverdue: false })).toBe('warn');
    expect(installmentTone({ status: 'Paid', isOverdue: false })).toBe('ok');
  });

  it('turns audit events into readable history', () => {
    const event = (action: string, changes: LedgerAuditEventDto['changes'], reason: string | null = null): LedgerAuditEventDto => ({
      id: action, entityType: 'Reservation', entityId: 'r', action, actorType: 'Agency', actorId: 'u', reason, changes,
      occurredAt: '2026-12-10T10:00:00Z',
    });
    const items = auditTimeline([
      event('PaymentVoided', { amount: { to: 400 } }, 'typo'),
      event('StatusChanged', { status: { from: 'Confirmed', to: 'Cancelled' } }),
      event('Updated', { operatorTotal: { from: 2000, to: 1800 }, holderName: { changed: true } }),
      event('DocumentUploaded', { fileName: { to: 'invoice.pdf' } }),
      event('SomethingNew', {}),
    ], 'EUR');

    expect(items[0].title).toBe('Анулирано плащане');
    expect(items[0].detail).toContain('400,00');
    expect(items[0].note).toBe('typo');
    expect(items[0].tone).toBe('bad');
    expect(items[1].detail).toBe('от „потвърдена“ на „анулирана“');
    expect(items[2].detail).toContain('1800,00');
    expect(items[3].detail).toBe('invoice.pdf');
    expect(items[4].title).toBe('SomethingNew');
    expect(items[4].meta).toContain('Служител на агенцията');
  });
});

describe('ledger screens', () => {
  it('maps the due views to the API filters', () => {
    expect(dueFilter('all')).toEqual({ dueWithinDays: null, overdueOnly: false });
    expect(dueFilter('overdue')).toEqual({ dueWithinDays: null, overdueOnly: true });
    expect(dueFilter('week')).toEqual({ dueWithinDays: 7, overdueOnly: false });
    expect(dueFilter('month')).toEqual({ dueWithinDays: 30, overdueOnly: false });
  });

  it('offers only the status changes that make sense', () => {
    expect(statusActions('Draft').map(a => a.status)).toEqual(['Confirmed', 'Cancelled']);
    expect(statusActions('Confirmed').map(a => a.status)).toEqual(['Completed', 'Cancelled']);
    expect(statusActions('Completed').map(a => a.status)).toEqual(['Confirmed']);
    expect(statusActions('Cancelled').map(a => a.status)).toEqual(['Confirmed']);
  });

  it('suggests what the first open instalment still needs', () => {
    const installments = [{ remainingAmount: 0 }, { remainingAmount: 200 }, { remainingAmount: 1400 }] as LedgerReservationDto['installments'];
    expect(suggestedPayment({ installments, outstanding: 1600 })).toBe(200);
    expect(suggestedPayment({ installments: [], outstanding: 900 })).toBe(900);
  });

  it('round-trips the commission between the API shape and the form', () => {
    const base = { tourOperatorId: 'o', reference: 'R', holderName: 'H', bookedOn: 'b', travelStart: 's', travelEnd: null,
      operatorTotal: 2000, currency: 'EUR', customerSaleAmount: null, status: 'Confirmed', notes: null } as unknown as LedgerReservationDto;

    const percent = formOf({ ...base, commission: { type: 'Percent', rate: 7, base: 'CustomerSale', expectedAmount: 161, receivedAmount: 0, status: 'Expected' } });
    expect(commissionOf(percent)).toEqual({ type: 'Percent', rate: 7, base: 'CustomerSale', amount: null });

    const fixed = formOf({ ...base, commission: { type: 'Fixed', rate: null, base: 'Manual', expectedAmount: 75, receivedAmount: 0, status: 'Expected' } });
    expect(commissionOf(fixed)).toEqual({ type: 'Fixed', rate: null, base: 'Manual', amount: 75 });

    expect(commissionOf(formOf({ ...base, commission: null }))).toBeNull();
  });
});

describe('ledger overview', () => {
  const totals = [
    { currency: 'EUR', outstanding: 2600, overdue: 200, dueIn7Days: 300, dueIn30Days: 1000, commissionExpected: 56, commissionReceived: 20 },
    { currency: 'USD', outstanding: 500, overdue: 0, dueIn7Days: 500, dueIn30Days: 500, commissionExpected: 0, commissionReceived: 0 },
  ];

  it('lists a figure per currency instead of adding currencies together', () => {
    expect(perCurrency(totals, row => row.dueIn7Days)).toMatch(/^300,00.+ · 500,00/);
    expect(perCurrency(totals, row => row.overdue)).toMatch(/^200,00\s?€$/);
    expect(perCurrency([], row => row.overdue)).toBe('');
  });

  it('writes file sizes the way people read them', () => {
    expect(fileSize(300)).toBe('1 KB');
    expect(fileSize(48_000)).toBe('47 KB');
    expect(fileSize(2_500_000)).toBe('2.4 MB');
  });
});
