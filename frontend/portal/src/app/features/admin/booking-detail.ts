import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, input, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { OperatorBooking, OperatorPayment, PaymentMethod, RecordPaymentRequest } from '../../core/api.types';
import { AuthService } from '../../core/auth.service';
import { formatDate, formatDateTime, formatMoney } from '../../core/format';
import { problemMessage } from '../../core/problem';
import { operatorActions, parseBreakdown } from './booking-actions';

/** One reservation from the operator's side: the net side, the money and the decisions. */
@Component({
  selector: 'app-admin-booking-detail',
  imports: [FormsModule, RouterLink],
  templateUrl: './booking-detail.html',
  styles: `
    .head { display: flex; align-items: baseline; gap: 1rem; flex-wrap: wrap; }
    .head h2 { margin: 0; }
    .chip { padding: .2rem .55rem; border-radius: 1rem; font-size: .75rem; font-weight: 600; background: var(--surface-2); }
    .chip.s-Confirmed { background: #e2f4e5; color: #1b6e2a; }
    .chip.s-AwaitingPayment { background: #fff4e0; color: #8a5a00; }
    .chip.s-Cancelled, .chip.s-Failed, .chip.s-Rejected, .chip.s-Expired { background: #fbe4e4; color: #a32020; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(22rem, 1fr)); gap: 1rem; margin-top: 1rem; }
    .card { background: var(--surface-1); border: 1px solid var(--border); border-radius: .75rem; padding: 1.25rem; }
    .card h3 { margin-top: 0; }
    dl { display: grid; grid-template-columns: max-content 1fr; gap: .3rem 1rem; margin: 0; }
    dt { color: var(--text-muted); font-size: .85rem; }
    dd { margin: 0; }
    table { width: 100%; border-collapse: collapse; }
    th, td { text-align: left; padding: .45rem .5rem; border-bottom: 1px solid var(--border); font-size: .9rem; }
    th.num, td.num { text-align: right; }
    .void { text-decoration: line-through; color: var(--text-muted); }
    .strong { font-weight: 700; }
    .muted { color: var(--text-muted); font-size: .85rem; }
    .actions { display: flex; flex-wrap: wrap; gap: .6rem; margin-top: 1rem; }
    .danger { background: var(--danger); }
    form.payment { display: grid; grid-template-columns: repeat(auto-fit, minmax(9rem, 1fr)); gap: .6rem; align-items: end; }
    .field { display: flex; flex-direction: column; gap: .25rem; }
    .field label { font-size: .75rem; font-weight: 600; color: var(--text-muted); }
    select { padding: .5rem; border: 1px solid var(--border); border-radius: .45rem; background: var(--surface-1); font: inherit; }
    .error { color: var(--danger); }
    .ok { color: #1b6e2a; }
  `,
})
export class AdminBookingDetail {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  /** Bound from the route parameter by withComponentInputBinding(). */
  readonly id = input.required<string>();

  readonly booking = signal<OperatorBooking | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);

  readonly loaded = resource({
    params: () => ({ id: this.id() }),
    loader: async ({ params }) => {
      const booking = await firstValueFrom(
        this.http.get<OperatorBooking>(`/api/v1/admin/bookings/${params.id}`));
      this.booking.set(booking);
      this.paymentAmount = booking.balanceDue;
      this.paymentCurrency = booking.currency;
      return booking;
    },
  });

  readonly isAdmin = computed(() => this.auth.user()?.role === 'Admin');
  readonly actions = computed(() => {
    const b = this.booking();
    return b ? operatorActions(b, this.isAdmin(), new Date()) : null;
  });
  readonly breakdown = computed(() => parseBreakdown(this.booking()?.breakdown));

  // Payment form. The amount is prefilled with the API's balance due — never computed here.
  paymentAmount = 0;
  paymentCurrency = 'EUR';
  paymentMethod: PaymentMethod = 'BankTransfer';
  paymentReceivedOn = new Date().toISOString().slice(0, 10);
  paymentReference = '';
  paymentNote = '';
  decisionNote = '';

  readonly fmtMoney = formatMoney;
  readonly fmtDate = formatDate;
  readonly fmtDateTime = formatDateTime;

  async recordPayment(): Promise<void> {
    const b = this.booking();
    if (!b) return;
    const request: RecordPaymentRequest = {
      amount: this.paymentAmount,
      currency: this.paymentCurrency,
      method: this.paymentMethod,
      receivedOn: this.paymentReceivedOn,
      reference: this.paymentReference.trim() || null,
      note: this.paymentNote.trim() || null,
    };
    await this.act(`/api/v1/admin/bookings/${b.id}/payments`, request, 'Payment recorded.');
    this.paymentReference = '';
    this.paymentNote = '';
  }

  async confirm(): Promise<void> {
    const b = this.booking();
    if (!b || !window.confirm(`Confirm reservation ${b.ref}? The agency will be able to receive its invoice.`)) return;
    await this.act(`/api/v1/admin/bookings/${b.id}/confirm`, {}, 'Reservation confirmed.');
  }

  async reject(): Promise<void> {
    await this.decide('reject', 'Reject reservation', 'Reservation rejected; the supplier hold was released.');
  }

  async cancel(): Promise<void> {
    await this.decide('cancel', 'Cancel reservation', 'Reservation cancelled.');
  }

  async expire(): Promise<void> {
    const b = this.booking();
    if (!b || !window.confirm(`Expire ${b.ref}? The supplier hold will be released.`)) return;
    await this.act(`/api/v1/admin/bookings/${b.id}/expire`, {}, 'Reservation expired.');
  }

  async voidPayment(payment: OperatorPayment): Promise<void> {
    const b = this.booking();
    const reason = b ? window.prompt(`Void payment of ${formatMoney(payment.amount, payment.currency)}? Reason:`) : null;
    if (!b || !reason?.trim()) return;
    await this.act(`/api/v1/admin/bookings/${b.id}/payments/${payment.id}/void`, { reason: reason.trim() }, 'Payment voided.');
  }

  private async decide(action: 'reject' | 'cancel', title: string, done: string): Promise<void> {
    const b = this.booking();
    if (!b) return;
    const note = this.decisionNote.trim();
    if (!note) {
      this.error.set(`${title}: a note is required.`);
      return;
    }
    if (!window.confirm(`${title} ${b.ref}? This cannot be undone.`)) return;
    await this.act(`/api/v1/admin/bookings/${b.id}/${action}`, { note }, done);
    this.decisionNote = '';
  }

  private async act(url: string, body: unknown, done: string): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    this.notice.set(null);
    try {
      const updated = await firstValueFrom(this.http.post<OperatorBooking>(url, body));
      this.booking.set(updated);
      this.paymentAmount = updated.balanceDue;
      this.notice.set(done);
    } catch (err) {
      this.error.set(problemMessage(err, 'The action failed.'));
    } finally {
      this.busy.set(false);
    }
  }
}
