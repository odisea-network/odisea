import { Component, computed, input } from '@angular/core';
import { Icon } from '../icon/icon';
import { IconName } from '../icon/icons';

export interface StatusMeta {
  label: string;
  icon: IconName;
  /** Visual key; see status-chip.scss. */
  tone: string;
}

/** Shape + label + colour for every reservation status, so greyscale and colour-blind users read it too. */
const BOOKING_STATUS: Record<string, StatusMeta> = {
  Draft: { label: 'Draft', icon: 'pencil', tone: 'draft' },
  PriceConfirmed: { label: 'Price confirmed', icon: 'tag', tone: 'priced' },
  PendingConfirmation: { label: 'Pending confirmation', icon: 'hourglass', tone: 'pending' },
  AwaitingPayment: { label: 'Awaiting payment', icon: 'clock', tone: 'await' },
  Confirmed: { label: 'Confirmed', icon: 'check-circle', tone: 'confirmed' },
  Rejected: { label: 'Rejected', icon: 'x-circle', tone: 'rejected' },
  Expired: { label: 'Expired', icon: 'stopwatch', tone: 'expired' },
  Cancelled: { label: 'Cancelled', icon: 'slash-circle', tone: 'cancelled' },
  Failed: { label: 'Failed', icon: 'alert-triangle', tone: 'failed' },
};

export function bookingStatusMeta(status: string): StatusMeta {
  return BOOKING_STATUS[status] ?? { label: status, icon: 'info', tone: 'draft' };
}

export function bookingStatusLabel(status: string): string {
  return bookingStatusMeta(status).label;
}

@Component({
  selector: 'ui-status-chip',
  imports: [Icon],
  template: `<ui-icon [name]="meta().icon" size="sm" />{{ meta().label }}`,
  styleUrl: './status-chip.scss',
  host: { '[class]': '"chip chip--" + meta().tone' },
})
export class StatusChip {
  readonly status = input.required<string>();
  protected readonly meta = computed(() => bookingStatusMeta(this.status()));
}
