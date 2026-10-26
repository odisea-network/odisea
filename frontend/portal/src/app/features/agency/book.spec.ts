import { describe, expect, it } from 'vitest';
import { BookingDto, PassengerDto } from '../../core/api.types';
import { contextFromBooking, formFromPassenger } from './book';

const passenger: PassengerDto = {
  id: 'p1', title: 'Mr', gender: 'Male', firstName: 'Dimitar', lastName: 'Georgiev',
  birthDate: '1986-03-12', passengerType: 'Adult', nationality: 'BG', documentType: 'Passport',
  documentNumber: '382114471', documentIssuingCountry: 'BG', documentExpiryDate: '2031-06-04',
  isLead: true, email: 'd@mail.bg', phone: '+359 88 412 3390',
};

const draft: BookingDto = {
  id: 'b1', ref: 'ODI-2027-000009', status: 'Draft', programName: 'Antalya Easter 2027',
  hotelName: 'Mock Palace Resort', checkIn: '2027-03-30', checkOut: '2027-04-06', adults: 2, children: 0,
  sellPrice: 831.1, agencyCommission: 83.11, currency: 'EUR', paidAmount: 0, balanceDue: 831.1,
  paymentDueAt: null, providerBookingRef: null, createdAt: '2026-10-26T10:00:00Z',
  passengers: [passenger], history: [],
};

describe('resuming a draft', () => {
  it('rebuilds the wizard context from the booking, nights included', () => {
    const ctx = contextFromBooking(draft);
    expect(ctx.nights).toBe(7);
    expect(ctx.adults).toBe(2);
    expect(ctx.offer.hotelName).toBe('Mock Palace Resort');
    // The search price is what the re-price is compared against: the draft's own price.
    expect(ctx.searchSellPrice).toBe(831.1);
  });

  it('maps a saved passenger back onto the form, dates as yyyy-mm-dd', () => {
    const form = formFromPassenger(passenger);
    expect(form.gender).toBe('Male');
    expect(form.documentType).toBe('Passport');
    expect(form.birthDate).toBe('1986-03-12');
    expect(form.email).toBe('d@mail.bg');
  });

  it('leaves unknown enum values empty instead of guessing', () => {
    const form = formFromPassenger({ ...passenger, gender: 'X', documentType: 'Visa', birthDate: null });
    expect(form.gender).toBe('');
    expect(form.documentType).toBe('');
    expect(form.birthDate).toBe('');
  });
});
