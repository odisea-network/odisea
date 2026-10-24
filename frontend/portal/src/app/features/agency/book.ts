import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { BookingDto, RePriceResponse } from '../../core/api.types';
import { formatDate, formatDateTime, formatMoney } from '../../core/format';
import { problemMessage } from '../../core/problem';
import { BookingDraftService } from './booking-draft.service';
import {
  PassengerForm, emptyPassenger, passengerTypeAt, toPassengerRequest, validatePassengers,
} from './passenger-form';

/**
 * Agency wizard: passengers → live price → submit. Submitting hands the reservation
 * to the supplier and starts the payment clock; the operator confirms once paid.
 */
@Component({
  selector: 'app-agency-book',
  imports: [FormsModule, RouterLink],
  templateUrl: './book.html',
  styles: `
    .card { background: var(--surface-1); border: 1px solid var(--border); border-radius: .75rem; padding: 1.25rem; max-width: 60rem; }
    .passenger { border: 1px solid var(--border); border-radius: .6rem; padding: 1rem; margin-bottom: 1rem; }
    .passenger header { display: flex; align-items: center; gap: 1rem; margin-bottom: .75rem; }
    .passenger header h4 { margin: 0; flex: 1; }
    .passenger header label { font-size: .85rem; display: flex; align-items: center; gap: .35rem; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr)); gap: .6rem; }
    .field { display: flex; flex-direction: column; gap: .25rem; }
    .field label { font-size: .75rem; font-weight: 600; color: var(--text-muted); }
    select { padding: .5rem; border: 1px solid var(--border); border-radius: .45rem; background: var(--surface-1); font: inherit; }
    .hint { font-size: .8rem; color: var(--text-muted); }
    .problems { margin: .75rem 0 0; padding: .7rem .9rem; border-radius: .5rem; background: #fff4e0; border: 1px solid #f0c36d; font-size: .85rem; }
    .problems li { margin: .15rem 0; }
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
  readonly step = signal<'passengers' | 'price' | 'done'>('passengers');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly booking = signal<BookingDto | null>(null);
  readonly drifted = signal(false);

  /** Set once the draft exists so later edits replace its passengers instead of
   *  creating a second (orphan) draft. */
  private draftId: string | null = null;

  // Exactly the party the offer was priced for: adults first, then children.
  passengers: PassengerForm[] = Array.from(
    { length: (this.context?.adults ?? 1) + (this.context?.children ?? 0) }, emptyPassenger);
  leadIndex = 0;

  readonly fmtMoney = formatMoney;
  readonly fmtDate = formatDate;
  readonly fmtDateTime = formatDateTime;

  slotLabel(index: number): string {
    if (!this.context) return `Passenger ${index + 1}`;
    return index < this.context.adults
      ? `Adult ${index + 1}`
      : `Child ${index - this.context.adults + 1}`;
  }

  typeHint(p: PassengerForm): string {
    return this.context && p.birthDate && p.birthDate <= this.context.checkIn
      ? passengerTypeAt(p.birthDate, this.context.checkIn)
      : '';
  }

  problems(): string[] {
    return this.context
      ? validatePassengers(this.passengers, this.leadIndex, this.context)
      : [];
  }

  async continueToPrice(): Promise<void> {
    if (!this.context || this.problems().length > 0) return;
    const ctx = this.context;
    const passengers = this.passengers.map((p, i) => toPassengerRequest(p, i === this.leadIndex));
    await this.run(async () => {
      if (this.draftId === null) {
        const created = await firstValueFrom(this.http.post<BookingDto>('/api/v1/bookings', {
          programId: ctx.programId,
          departureId: ctx.departureId,
          hotelId: ctx.offer.hotelId,
          offerToken: ctx.offer.offerToken,
          passengers,
        }));
        this.draftId = created.id;
      } else {
        await firstValueFrom(
          this.http.put<BookingDto>(`/api/v1/bookings/${this.draftId}/passengers`, { passengers }),
        );
      }
      const priced = await firstValueFrom(
        this.http.post<RePriceResponse>(`/api/v1/bookings/${this.draftId}/re-price`, {}),
      );
      this.booking.set(priced.booking);
      // Compare against the SEARCH price the agent saw, not just the draft.
      this.drifted.set(priced.booking.sellPrice !== ctx.searchSellPrice);
      this.step.set('price');
    });
  }

  editPassengers(): void {
    this.step.set('passengers');
  }

  async submit(): Promise<void> {
    const current = this.booking();
    if (!current) return;
    await this.run(async () => {
      this.booking.set(await firstValueFrom(
        this.http.post<BookingDto>(`/api/v1/bookings/${current.id}/submit`, {}),
      ));
      this.step.set('done');
      this.draft.clear();
    });
  }

  async cancel(): Promise<void> {
    const id = this.booking()?.id ?? this.draftId;
    if (!id) return;
    await this.run(async () => {
      await firstValueFrom(this.http.post(`/api/v1/bookings/${id}/cancel`, {}));
      this.draftId = null;
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
      this.error.set(problemMessage(err, 'Something went wrong.'));
    } finally {
      this.busy.set(false);
    }
  }
}
