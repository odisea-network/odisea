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

// --- Operator-only shapes (served by /api/v1/admin/** and /api/v1/agencies) ---

export interface AgencyDto {
  id: string;
  name: string;
  country: string;
  status: string;
  creditLimit: number;
  contactEmail: string | null;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface OperatorBookingSummary {
  id: string;
  ref: string;
  status: BookingStatus | string;
  agencyId: string;
  programName: string;
  hotelName: string;
  checkIn: string;
  checkOut: string;
  adults: number;
  children: number;
  sellAmount: number | null;
  paidAmount: number;
  balanceDue: number;
  currency: string;
  paymentDueAt: string | null;
  createdAt: string;
}

export interface OperatorStatusHistory {
  at: string;
  from: string;
  to: string;
  note: string | null;
  actorType: string;
  actorUserId: string | null;
}

export type PaymentMethod = 'BankTransfer' | 'Card' | 'Cash' | 'Other';

export interface OperatorPayment {
  id: string;
  amount: number;
  currency: string;
  method: PaymentMethod | string;
  reference: string | null;
  receivedOn: string;
  recordedByUserId: string;
  note: string | null;
  recordedAt: string;
  voidedAt: string | null;
  voidedByUserId: string | null;
  voidReason: string | null;
}

export interface OperatorBooking extends OperatorBookingSummary {
  agentUserId: string;
  programId: string;
  departureId: string;
  hotelId: string;
  providerCode: string;
  providerBookingRef: string | null;
  netAmount: number | null;
  commissionAmount: number | null;
  /** The pricing breakdown persisted at re-price time, verbatim (PascalCase keys). */
  breakdown: unknown;
  passengers: PassengerDto[];
  payments: OperatorPayment[];
  history: OperatorStatusHistory[];
}

export interface RecordPaymentRequest {
  amount: number;
  currency: string;
  method: PaymentMethod;
  receivedOn: string;
  reference: string | null;
  note: string | null;
}

// --- Agencies admin (served by /api/v1/agencies/**) ---

export type AgencyStatus = 'Active' | 'Suspended';
export type AgencyPlan = 'Core' | 'Workspace';
export type AgencyUserRole = 'AgencyAdmin' | 'Agent';

export interface AgencyBillingDto {
  legalName: string | null;
  eik: string | null;
  vatNumber: string | null;
  address: string | null;
  city: string | null;
  mol: string | null;
}

export interface AgencyDetailDto extends AgencyDto {
  plan: AgencyPlan | string;
  commissionLevelId: string | null;
  billing: AgencyBillingDto | null;
  createdAt: string;
}

export interface SaveAgencyRequest {
  name: string;
  country: string;
  creditLimit: number;
  contactEmail: string | null;
}

export interface UpdateAgencyRequest extends SaveAgencyRequest {
  status: AgencyStatus;
}

export interface UpdateAgencyBillingRequest {
  legalName: string;
  eik: string;
  vatNumber: string | null;
  address: string;
  city: string;
  mol: string;
}

export interface AgencyUserDto {
  id: string;
  agencyId: string;
  email: string;
  fullName: string;
  role: AgencyUserRole | string;
  isActive: boolean;
  createdAt: string;
}

export interface CreateAgencyUserRequest {
  email: string;
  fullName: string;
  role: AgencyUserRole;
  password: string;
}

export interface ApiKeyDto {
  id: string;
  agencyId: string;
  name: string;
  keyPrefix: string;
  createdAt: string;
  expiresAt: string | null;
  revokedAt: string | null;
  lastUsedAt: string | null;
}

/** Returned once from the create call; the secret is never shown again. */
export interface ApiKeyCreatedDto {
  key: ApiKeyDto;
  secret: string;
}

export interface UpdateBrandingRequest {
  displayName: string;
  primaryColor: string;
  accentColor: string;
  supportEmail: string | null;
  supportPhone: string | null;
  legalFooter: string | null;
}

// --- Catalog admin (served by /api/v1/destinations, /api/v1/hotels, /api/v1/programs) ---

export type ProgramStatus = 'Draft' | 'Published' | 'Archived';

export interface DestinationDto {
  id: string;
  name: string;
  country: string;
  createdAt: string;
}

export interface LocationMappingDto {
  id: string;
  destinationId: string;
  providerCode: string;
  externalLocationCode: string;
}

export interface HotelDto {
  id: string;
  name: string;
  destinationId: string;
  stars: number;
  createdAt: string;
}

export interface HotelMappingDto {
  id: string;
  hotelId: string;
  providerCode: string;
  externalHotelCode: string;
}

export interface ProgramDto extends ProgramListItem {
  createdAt: string;
}

export interface CreateProgramRequest {
  name: string;
  destinationId: string;
  providerCode: string;
  season: string | null;
  description: string | null;
}

export interface UpdateProgramRequest {
  name: string;
  season: string | null;
  description: string | null;
}

export interface CreateDepartureRequest {
  startDate: string;
  endDate: string;
  transportNote: string | null;
}

// --- Pricing admin (served by /api/v1/pricing-rules, /api/v1/exchange-rates) ---

export type RuleScope = 'Global' | 'Program' | 'Agency';
export type RuleKind = 'Markup' | 'Fee' | 'Commission';
export type RuleValueType = 'Percent' | 'FixedAmount';

export interface PricingRuleDto {
  id: string;
  name: string;
  scope: RuleScope | string;
  scopeId: string | null;
  kind: RuleKind | string;
  valueType: RuleValueType | string;
  value: number;
  priority: number;
  validFrom: string | null;
  validTo: string | null;
  enabled: boolean;
}

export interface SavePricingRuleRequest {
  name: string;
  scope: RuleScope;
  scopeId: string | null;
  kind: RuleKind;
  valueType: RuleValueType;
  value: number;
  priority: number;
  validFrom: string | null;
  validTo: string | null;
  enabled: boolean;
}

export interface ExchangeRateDto {
  id: string;
  fromCurrency: string;
  toCurrency: string;
  rate: number;
  asOf: string;
}

export interface SaveExchangeRateRequest {
  fromCurrency: string;
  rate: number;
  asOf: string;
}
