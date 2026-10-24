import { Gender, PassengerRequest, PassengerType, TravelDocumentType } from '../../core/api.types';

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
    problems.push(`The offer is for ${context.adults} adult(s) and ${context.children} child(ren): ${passengers.length} passenger(s) entered.`);
  }
  if (leadIndex < 0 || leadIndex >= passengers.length) {
    problems.push('Choose a lead passenger.');
  }

  let adults = 0;
  let minors = 0;
  passengers.forEach((p, i) => {
    const label = `Passenger ${i + 1}`;
    const missing: string[] = [];
    if (!p.gender) missing.push('gender');
    if (!p.firstName.trim()) missing.push('first name');
    else if (!LATIN_NAME.test(p.firstName.trim())) problems.push(`${label}: first name must use Latin letters as in the travel document.`);
    if (!p.lastName.trim()) missing.push('last name');
    else if (!LATIN_NAME.test(p.lastName.trim())) problems.push(`${label}: last name must use Latin letters as in the travel document.`);
    if (!p.birthDate) missing.push('birth date');
    else if (p.birthDate > context.checkIn) problems.push(`${label}: birth date is after check-in.`);
    if (!p.nationality.trim()) missing.push('nationality');
    else if (!COUNTRY_CODE.test(p.nationality.trim())) problems.push(`${label}: nationality must be a 2-letter country code.`);
    if (!p.documentType) missing.push('document type');
    if (!p.documentNumber.trim()) missing.push('document number');
    else if (!DOCUMENT_NUMBER.test(p.documentNumber.trim())) problems.push(`${label}: document number must be letters and digits only (max 20).`);
    if (!p.documentIssuingCountry.trim()) missing.push('document issuing country');
    else if (!COUNTRY_CODE.test(p.documentIssuingCountry.trim())) problems.push(`${label}: issuing country must be a 2-letter country code.`);
    if (!p.documentExpiryDate) missing.push('document expiry date');
    else if (p.documentExpiryDate < context.checkOut) problems.push(`${label}: travel document expires before check-out.`);
    if (i === leadIndex) {
      if (!p.email.trim()) missing.push('email (lead)');
      else if (!EMAIL.test(p.email.trim())) problems.push(`${label}: email looks invalid.`);
      if (!p.phone.trim()) missing.push('phone (lead)');
      else if (!PHONE.test(p.phone.trim())) problems.push(`${label}: phone may contain digits, spaces, brackets and a leading +.`);
    }
    if (missing.length) problems.push(`${label}: missing ${missing.join(', ')}.`);

    if (p.birthDate && p.birthDate <= context.checkIn) {
      if (passengerTypeAt(p.birthDate, context.checkIn) === 'Adult') adults++;
      else minors++;
    }
  });

  if (problems.length === 0 && (adults !== context.adults || minors !== context.children)) {
    problems.push(`Birth dates give ${adults} adult(s) and ${minors} child(ren), but the offer is for ${context.adults} adult(s) and ${context.children} child(ren).`);
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
