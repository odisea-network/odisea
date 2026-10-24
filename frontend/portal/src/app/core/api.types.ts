// Mirrors of the backend DTOs (camelCase over the wire). Money fields are
// numbers for DISPLAY only — the frontend never computes prices.

export interface BrandingDto {
  displayName: string;
  primaryColor: string;
  accentColor: string;
  supportEmail: string | null;
  supportPhone: string | null;
  legalFooter: string | null;
  hasLogo: boolean;
  logoUrl: string | null;
  logoVersion: string | null;
  updatedAt: string | null;
}

export interface ProgramListItem {
  id: string;
  name: string;
  destinationId: string;
  providerCode: string;
  season: string | null;
  description: string | null;
  status: string;
}

export interface DepartureDto {
  id: string;
  startDate: string;
  endDate: string;
  nights: number;
  transportNote: string | null;
}

export interface ProgramDetail extends ProgramListItem {
  departures: DepartureDto[];
  hotelIds: string[];
}

export interface HotelOffer {
  hotelId: string;
  hotelName: string;
  stars: number;
  roomType: string;
  board: string;
  sellPrice: number;
  currency: string;
  agencyCommission: number;
  offerToken: string;
  offerExpiresAt: string | null;
}

export interface SearchResponse {
  programId: string;
  programName: string;
  departureId: string;
  checkIn: string;
  checkOut: string;
  nights: number;
  offers: HotelOffer[];
}

export type Gender = 'Male' | 'Female';
export type TravelDocumentType = 'Passport' | 'IdCard';
export type PassengerType = 'Adult' | 'Child' | 'Infant';

/** What the API accepts for a passenger (POST /bookings, PUT /bookings/{id}/passengers). */
export interface PassengerRequest {
  title: string | null;
  gender: Gender | null;
  firstName: string;
  lastName: string;
  birthDate: string | null;
  nationality: string | null;
  documentType: TravelDocumentType | null;
  documentNumber: string | null;
  documentIssuingCountry: string | null;
  documentExpiryDate: string | null;
  isLead: boolean;
  email: string | null;
  phone: string | null;
}

export interface PassengerDto {
  id: string;
  title: string | null;
  gender: string | null;
  firstName: string;
  lastName: string;
  birthDate: string | null;
  passengerType: string | null;
  nationality: string | null;
  documentType: string | null;
  documentNumber: string | null;
  documentIssuingCountry: string | null;
  documentExpiryDate: string | null;
  isLead: boolean;
  email: string | null;
  phone: string | null;
}

export interface StatusHistoryDto {
  at: string;
  from: string;
  to: string;
  note: string | null;
  /** Agency | ApiClient | Operator | System — never an operator's identity. */
  actor: string;
}

export type BookingStatus =
  | 'Draft'
  | 'PriceConfirmed'
  | 'PendingConfirmation'
  | 'AwaitingPayment'
  | 'Confirmed'
  | 'Rejected'
  | 'Expired'
  | 'Cancelled'
  | 'Failed';

export interface BookingDto {
  id: string;
  ref: string;
  status: BookingStatus | string;
  programName: string;
  hotelName: string;
  checkIn: string;
  checkOut: string;
  adults: number;
  children: number;
  sellPrice: number | null;
  agencyCommission: number | null;
  currency: string;
  paidAmount: number;
  balanceDue: number;
  paymentDueAt: string | null;
  providerBookingRef: string | null;
  createdAt: string;
  passengers: PassengerDto[];
  history: StatusHistoryDto[];
}

export interface RePriceResponse {
  booking: BookingDto;
  priceChanged: boolean;
  previousSellPrice: number | null;
}

export interface FinancialDocumentDto {
  id: string;
  number: string;
  kind: string;
  status: string;
  issueDate: string;
  taxEventDate: string;
  bookingRef: string | null;
  relatedDocumentNumber: string | null;
  total: number;
  currency: string;
  description: string;
  dueDate: string | null;
}

export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
}
