import { Gender, PassengerRequest, PassengerType, TravelDocumentType } from '../../core/api.types';
import { t } from '../../core/i18n/i18n';

/** One row of the wizard. Everything is a string so it binds straight to inputs. */
export interface PassengerForm {
  title: string;
  gender: Gender | '';
  firstName: string;
  lastName: string;
  birthDate: string;
  nationality: string;
  documentType: TravelDocumentType | '';
  documentNumber: string;
  documentIssuingCountry: string;
  documentExpiryDate: string;
  email: string;
  phone: string;
}

export function emptyPassenger(): PassengerForm {
  return {
    title: '', gender: '', firstName: '', lastName: '', birthDate: '', nationality: '',
    documentType: '', documentNumber: '', documentIssuingCountry: '', documentExpiryDate: '',
    email: '', phone: '',
  };
}

// The same shapes the API validates, so the agent hears about a problem before the round trip.
export const LATIN_NAME = /^[A-Za-zÀ-ɏ][A-Za-zÀ-ɏ '\-]*$/;
export const COUNTRY_CODE = /^[A-Za-z]{2}$/;
export const DOCUMENT_NUMBER = /^[A-Za-z0-9]{1,20}$/;
export const PHONE = /^\+?[0-9 ()\-]{6,30}$/;
const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** Mirrors the backend rule: completed years at check-in, under 2 → Infant, under 12 → Child. */
export function passengerTypeAt(birthDate: string, checkIn: string): PassengerType {
  const [by, bm, bd] = birthDate.split('-').map(Number);
  const [cy, cm, cd] = checkIn.split('-').map(Number);
  let age = cy - by;
  if (cm < bm || (cm === bm && cd < bd)) age--;
  if (age < 2) return 'Infant';
  if (age < 12) return 'Child';
  return 'Adult';
}

export interface OccupancyContext {
  adults: number;
  children: number;
  checkIn: string;
  checkOut: string;
}

/**
 * Everything submit will insist on, phrased per passenger position (never by name,
 * matching the API). An empty result means the booking can be submitted.
 */
export function validatePassengers(
  passengers: PassengerForm[], leadIndex: number, context: OccupancyContext,
): string[] {
  const problems: string[] = [];
  if (passengers.length !== context.adults + context.children) {
    problems.push(t('passenger.countMismatch', { adults: context.adults, children: context.children, count: passengers.length }));
  }
  if (leadIndex < 0 || leadIndex >= passengers.length) {
    problems.push(t('passenger.chooseLead'));
  }

  let adults = 0;
  let minors = 0;
  passengers.forEach((p, i) => {
    const who = t('passenger.label', { n: i + 1 });
    const missing: string[] = [];
    if (!p.gender) missing.push(t('passenger.field.gender'));
    if (!p.firstName.trim()) missing.push(t('passenger.field.firstName'));
    else if (!LATIN_NAME.test(p.firstName.trim())) problems.push(t('passenger.firstNameLatin', { who }));
    if (!p.lastName.trim()) missing.push(t('passenger.field.lastName'));
    else if (!LATIN_NAME.test(p.lastName.trim())) problems.push(t('passenger.lastNameLatin', { who }));
    if (!p.birthDate) missing.push(t('passenger.field.birthDate'));
    else if (p.birthDate > context.checkIn) problems.push(t('passenger.birthAfterCheckIn', { who }));
    if (!p.nationality.trim()) missing.push(t('passenger.field.nationality'));
    else if (!COUNTRY_CODE.test(p.nationality.trim())) problems.push(t('passenger.nationalityCode', { who }));
    if (!p.documentType) missing.push(t('passenger.field.documentType'));
    if (!p.documentNumber.trim()) missing.push(t('passenger.field.documentNumber'));
    else if (!DOCUMENT_NUMBER.test(p.documentNumber.trim())) problems.push(t('passenger.documentNumberFormat', { who }));
    if (!p.documentIssuingCountry.trim()) missing.push(t('passenger.field.issuingCountry'));
    else if (!COUNTRY_CODE.test(p.documentIssuingCountry.trim())) problems.push(t('passenger.issuingCountryCode', { who }));
    if (!p.documentExpiryDate) missing.push(t('passenger.field.expiry'));
    else if (p.documentExpiryDate < context.checkOut) problems.push(t('passenger.documentExpires', { who }));
    if (i === leadIndex) {
      if (!p.email.trim()) missing.push(t('passenger.field.email'));
      else if (!EMAIL.test(p.email.trim())) problems.push(t('passenger.emailInvalid', { who }));
      if (!p.phone.trim()) missing.push(t('passenger.field.phone'));
      else if (!PHONE.test(p.phone.trim())) problems.push(t('passenger.phoneInvalid', { who }));
    }
    if (missing.length) problems.push(t('passenger.missing', { who, fields: missing.join(', ') }));

    if (p.birthDate && p.birthDate <= context.checkIn) {
      if (passengerTypeAt(p.birthDate, context.checkIn) === 'Adult') adults++;
      else minors++;
    }
  });

  if (problems.length === 0 && (adults !== context.adults || minors !== context.children)) {
    problems.push(t('passenger.ageMismatch', {
      adults, minors, offerAdults: context.adults, offerChildren: context.children,
    }));
  }
  return problems;
}

export function toPassengerRequest(p: PassengerForm, isLead: boolean): PassengerRequest {
  const text = (value: string): string | null => (value.trim() === '' ? null : value.trim());
  const code = (value: string): string | null => text(value)?.toUpperCase() ?? null;
  return {
    title: text(p.title),
    gender: p.gender || null,
    firstName: p.firstName.trim(),
    lastName: p.lastName.trim(),
    birthDate: text(p.birthDate),
    nationality: code(p.nationality),
    documentType: p.documentType || null,
    documentNumber: code(p.documentNumber),
    documentIssuingCountry: code(p.documentIssuingCountry),
    documentExpiryDate: text(p.documentExpiryDate),
    isLead,
    email: isLead ? text(p.email) : null,
    phone: isLead ? text(p.phone) : null,
  };
}
