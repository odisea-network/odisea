import { describe, expect, it } from 'vitest';
import {
  OccupancyContext, PassengerForm, emptyPassenger, passengerTypeAt, toPassengerRequest, validatePassengers,
} from './passenger-form';

const context: OccupancyContext = { adults: 2, children: 0, checkIn: '2027-03-30', checkOut: '2027-04-06' };

function adult(overrides: Partial<PassengerForm> = {}): PassengerForm {
  return {
    ...emptyPassenger(),
    gender: 'Male', firstName: 'Ivan', lastName: 'Petrov', birthDate: '1985-05-01', nationality: 'bg',
    documentType: 'Passport', documentNumber: 'ab1234567', documentIssuingCountry: 'BG',
    documentExpiryDate: '2031-01-01', email: 'ivan@example.com', phone: '+359 888 123 456',
    ...overrides,
  };
}

describe('passengerTypeAt', () => {
  it('counts completed years at check-in', () => {
    expect(passengerTypeAt('2025-03-31', '2027-03-30')).toBe('Infant'); // turns 2 the day after
    expect(passengerTypeAt('2025-03-30', '2027-03-30')).toBe('Child');  // turns 2 on check-in day
    expect(passengerTypeAt('2015-03-31', '2027-03-30')).toBe('Child');
    expect(passengerTypeAt('2015-03-30', '2027-03-30')).toBe('Adult');
  });
});

describe('validatePassengers', () => {
  it('accepts a complete party', () => {
    expect(validatePassengers([adult(), adult({ firstName: 'Maria', lastName: 'Petrova' })], 0, context)).toEqual([]);
  });

  it('reports missing fields by position, never by name', () => {
    const problems = validatePassengers(
      [adult(), adult({ firstName: 'Nameless', nationality: '', documentNumber: '' })], 0, context);
    expect(problems).toEqual(['Пътник 2: не е попълнено: гражданство, номер на документа.']);
    expect(problems.join()).not.toContain('Nameless');
  });

  it('requires the lead to have contact details and exactly one lead to be chosen', () => {
    expect(validatePassengers([adult({ email: '', phone: '' }), adult()], 0, context))
      .toEqual(['Пътник 1: не е попълнено: имейл (титуляр), телефон (титуляр).']);
    expect(validatePassengers([adult(), adult()], -1, context)).toContain('Изберете титуляр.');
  });

  it('checks the party against the priced occupancy, by count and by age mix', () => {
    expect(validatePassengers([adult()], 0, context)[0]).toContain('възрастни: 2, деца: 0; въведени пътници: 1');
    expect(validatePassengers([adult(), adult({ birthDate: '2020-01-01' })], 0, context))
      .toEqual(['По дати на раждане: възрастни 1, деца 1; офертата е за възрастни 2, деца 0.']);
  });

  it('rejects documents that expire before check-out and non-Latin names', () => {
    const problems = validatePassengers(
      [adult({ documentExpiryDate: '2027-04-01' }), adult({ firstName: 'Иван' })], 0, context);
    expect(problems).toContain('Пътник 1: документът за пътуване изтича преди напускането.');
    expect(problems).toContain('Пътник 2: името трябва да е на латиница, както е в документа за пътуване.');
  });
});

describe('toPassengerRequest', () => {
  it('trims, upper-cases codes, nulls blanks and keeps contact only on the lead', () => {
    const lead = toPassengerRequest(adult({ title: ' Mr ', nationality: ' bg ' }), true);
    expect(lead.title).toBe('Mr');
    expect(lead.nationality).toBe('BG');
    expect(lead.documentNumber).toBe('AB1234567');
    expect(lead.isLead).toBe(true);
    expect(lead.email).toBe('ivan@example.com');

    const companion = toPassengerRequest(adult({ title: '' }), false);
    expect(companion.title).toBeNull();
    expect(companion.email).toBeNull();
    expect(companion.phone).toBeNull();
    expect(companion.isLead).toBe(false);
  });
});
