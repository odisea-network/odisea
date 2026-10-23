// Mirrors of the backend DTOs (camelCase over the wire). Money fields are
// numbers for DISPLAY only — the frontend never computes prices.

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

export interface ProgramDetail extends Omit<ProgramListItem, 'status'> {
  status: string;
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

export interface PassengerDto {
  id: string;
  firstName: string;
  lastName: string;
  birthDate: string | null;
  isLead: boolean;
}

export interface StatusHistoryDto {
  at: string;
  from: string;
  to: string;
  note: string | null;
}

export interface BookingDto {
  id: string;
  ref: string;
  status: string;
  programName: string;
  hotelName: string;
  checkIn: string;
  checkOut: string;
  sellPrice: number | null;
  agencyCommission: number | null;
  currency: string;
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
