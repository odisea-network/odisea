import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { BookingDto, ProblemDetails, RePriceResponse } from '../../core/api.types';
import { formatDate, formatMoney } from '../../core/format';
import { BookingDraftService } from './booking-draft.service';

interface PassengerForm {
  firstName: string;
  lastName: string;
  birthDate: string;
}

@Component({
  selector: 'app-agency-book',
  imports: [FormsModule, RouterLink],
  template: `
    @if (!context) {
      <p>No offer selected. <a routerLink="/agency/search">Back to search</a></p>
    } @else {
      <h2>Book: {{ context.programName }}</h2>
      <p class="muted">
        {{ context.offer.hotelName }} {{ '★'.repeat(context.offer.stars) }} ·
        {{ context.offer.roomType }} / {{ context.offer.board }} ·
        {{ fmtDate(context.checkIn) }} – {{ fmtDate(context.checkOut) }} ({{ context.nights }} nights)
      </p>

      @if (step() === 'passengers') {
        <section class="card">
          <h3>Passengers</h3>
          @for (p of passengers; track $index; let i = $index) {
            <div class="row">
              <span class="lead">{{ i === 0 ? 'Lead' : i + 1 }}</span>
              <input placeholder="First name" [(ngModel)]="p.firstName" name="fn{{ i }}" />
              <input placeholder="Last name" [(ngModel)]="p.lastName" name="ln{{ i }}" />
              <input type="date" [(ngModel)]="p.birthDate" name="bd{{ i }}" />
              @if (i > 0) {
                <button type="button" class="ghost" (click)="passengers.splice(i, 1)">Remove</button>
              }
            </div>
          }
          <div class="actions">
            <button type="button" class="ghost" (click)="addPassenger()" [disabled]="passengers.length >= 9">
              + Add passenger
            </button>
            <button type="button" (click)="createAndPrice()" [disabled]="busy() || !passengersValid()">
              {{ busy() ? 'Checking price…' : 'Continue — check live price' }}
            </button>
          </div>
          <p class="muted">
            Search price: <strong>{{ fmtMoney(context.searchSellPrice, context.offer.currency) }}</strong>
            — it will be re-verified with the supplier before you commit.
          </p>
        </section>
      }

      @if (step() === 'confirm' && booking(); as b) {
        <section class="card">
          <h3>Confirm price</h3>
          @if (drifted()) {
            <p class="drift" role="alert">
              ⚠ The price changed since your search:
              <s>{{ fmtMoney(context.searchSellPrice, b.currency) }}</s>
              → <strong>{{ fmtMoney(b.sellPrice, b.currency) }}</strong>. Confirm only if the client agrees.
            </p>
          } @else {
            <p>Price confirmed by the supplier.</p>
          }
          <p class="price">{{ fmtMoney(b.sellPrice, b.currency) }}</p>
          <p class="muted">Your commission: {{ fmtMoney(b.agencyCommission, b.currency) }} · Booking {{ b.ref }}</p>
          <div class="actions">
            <button type="button" class="ghost" (click)="cancel()" [disabled]="busy()">Cancel booking</button>
            <button type="button" (click)="confirm()" [disabled]="busy()">
              {{ busy() ? 'Booking…' : 'Confirm & book' }}
            </button>
          </div>
        </section>
      }

      @if (step() === 'done' && booking(); as b) {
        <section class="card done">
          @if (b.status === 'Confirmed') {
            <h3>✔ Booked</h3>
            <p><strong>{{ b.ref }}</strong> is confirmed — supplier reference {{ b.providerBookingRef }}.</p>
            <p>{{ fmtMoney(b.sellPrice, b.currency) }} · invoice follows from the operator.</p>
          } @else {
            <h3>Booking {{ b.status }}</h3>
            <p>{{ b.ref }} ended in status {{ b.status }}. See bookings for details.</p>
          }
          <a routerLink="/agency/bookings">Go to bookings →</a>
        </section>
      }

      @if (error()) {
        <p class="error" role="alert">{{ error() }}</p>
      }
    }
  `,
  styles: `
    .card { background: var(--surface-1); border: 1px solid var(--border); border-radius: .75rem; padding: 1.25rem; max-width: 44rem; }
    .row { display: flex; gap: .6rem; align-items: center; margin-bottom: .6rem; }
    .lead { width: 3rem; font-size: .8rem; color: var(--text-muted); }
    .actions { display: flex; gap: .75rem; justify-content: flex-end; margin-top: 1rem; }
    .price { font-size: 1.8rem; font-weight: 800; margin: .5rem 0; }
    .drift { background: #fff4e0; border: 1px solid #f0c36d; padding: .7rem .9rem; border-radius: .5rem; }
    .done h3 { margin-top: 0; }
    .muted { color: var(--text-muted); }
    .error { color: var(--danger); }
  `,
})
export class AgencyBook {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly draft = inject(BookingDraftService);

  readonly context = this.draft.context();
  readonly step = signal<'passengers' | 'confirm' | 'done'>('passengers');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly booking = signal<BookingDto | null>(null);
  readonly drifted = signal(false);

  passengers: PassengerForm[] = [{ firstName: '', lastName: '', birthDate: '' }];

  readonly fmtMoney = formatMoney;
  readonly fmtDate = formatDate;

  addPassenger(): void {
    this.passengers.push({ firstName: '', lastName: '', birthDate: '' });
  }

  passengersValid(): boolean {
    return this.passengers.every(p => p.firstName.trim() && p.lastName.trim());
  }

  async createAndPrice(): Promise<void> {
    if (!this.context) return;
    await this.run(async () => {
      const draft = await firstValueFrom(this.http.post<BookingDto>('/api/v1/bookings', {
        programId: this.context!.programId,
        departureId: this.context!.departureId,
        hotelId: this.context!.offer.hotelId,
        offerToken: this.context!.offer.offerToken,
        passengers: this.passengers.map((p, i) => ({
          firstName: p.firstName.trim(),
          lastName: p.lastName.trim(),
          birthDate: p.birthDate || null,
          isLead: i === 0,
        })),
      }));
      const priced = await firstValueFrom(
        this.http.post<RePriceResponse>(`/api/v1/bookings/${draft.id}/re-price`, {}),
      );
      this.booking.set(priced.booking);
      // Compare against the SEARCH price the agent saw, not just the draft.
      this.drifted.set(priced.booking.sellPrice !== this.context!.searchSellPrice);
      this.step.set('confirm');
    });
  }

  async confirm(): Promise<void> {
    const current = this.booking();
    if (!current) return;
    await this.run(async () => {
      this.booking.set(await firstValueFrom(
        this.http.post<BookingDto>(`/api/v1/bookings/${current.id}/confirm`, {}),
      ));
      this.step.set('done');
      this.draft.clear();
    });
  }

  async cancel(): Promise<void> {
    const current = this.booking();
    if (!current) return;
    await this.run(async () => {
      await firstValueFrom(this.http.post(`/api/v1/bookings/${current.id}/cancel`, {}));
      this.draft.clear();
      await this.router.navigate(['/agency/search']);
    });
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await action();
    } catch (err) {
      const problem = err instanceof HttpErrorResponse ? (err.error as ProblemDetails) : null;
      this.error.set(problem?.detail ?? problem?.title ?? 'Something went wrong.');
    } finally {
      this.busy.set(false);
    }
  }
}
