import { OperatorBooking } from '../../core/api.types';

/**
 * Which operator actions the UI offers for a booking. Mirrors the API's state and role
 * rules so buttons appear only when they can succeed; the API remains the authority.
 */
export interface OperatorActions {
  recordPayment: boolean;
  confirm: boolean;
  reject: boolean;
  expire: boolean;
  cancel: boolean;
  voidPayment: boolean;
}

export function operatorActions(
  b: Pick<OperatorBooking, 'status' | 'sellAmount' | 'balanceDue' | 'paymentDueAt'>,
  isAdmin: boolean,
  now: Date,
): OperatorActions {
  const awaiting = b.status === 'AwaitingPayment';
  // balanceDue is computed by the API; comparing it to zero is a display gate, not arithmetic.
  const fullyPaid = b.sellAmount !== null && b.balanceDue <= 0;
  const pastDeadline = b.paymentDueAt !== null && Date.parse(b.paymentDueAt) < now.getTime();
  return {
    recordPayment: awaiting,
    confirm: isAdmin && awaiting && fullyPaid,
    reject: awaiting,
    expire: awaiting && pastDeadline,
    cancel: isAdmin && (b.status === 'PendingConfirmation' || awaiting || b.status === 'Confirmed'),
    voidPayment: isAdmin && awaiting,
  };
}

export interface BreakdownStep {
  description: string;
  kind: string;
  before: number;
  after: number;
}

export interface Breakdown {
  currency: string;
  netCost: number;
  sellPrice: number;
  agencyCommission: number;
  steps: BreakdownStep[];
}

type MoneyJson = { Amount?: number; Currency?: string };
type StepJson = { Description?: string; Kind?: string; Before?: MoneyJson; After?: MoneyJson };
type BreakdownJson = { NetCost?: MoneyJson; SellPrice?: MoneyJson; AgencyCommission?: MoneyJson; Steps?: StepJson[] };

/** Reads the stored pricing breakdown (PascalCase, as the pricing engine serialised it). */
export function parseBreakdown(raw: unknown): Breakdown | null {
  if (!raw || typeof raw !== 'object') return null;
  const json = raw as BreakdownJson;
  const net = json.NetCost;
  const sell = json.SellPrice;
  if (!net || !sell || typeof net.Amount !== 'number' || typeof sell.Amount !== 'number') return null;
  return {
    currency: sell.Currency ?? net.Currency ?? 'EUR',
    netCost: net.Amount,
    sellPrice: sell.Amount,
    agencyCommission: json.AgencyCommission?.Amount ?? 0,
    steps: (json.Steps ?? []).map(step => ({
      description: step.Description ?? '',
      kind: step.Kind ?? '',
      before: step.Before?.Amount ?? 0,
      after: step.After?.Amount ?? 0,
    })),
  };
}
