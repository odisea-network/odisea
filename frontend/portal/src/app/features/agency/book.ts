import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { BookingDto, RePriceResponse } from '../../core/api.types';
import { formatDate, formatMoney } from '../../core/format';
import { problemMessage } from '../../core/problem';
import { BookingDraftService } from './booking-draft.service';

interface PassengerForm {
  firstName: string;
  lastName: string;
  birthDate: string;
}

@Component({
  selector: 'app-agency-book',
  imports: [FormsModule, RouterLink],
  templateUrl: './book.html',
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

  /** Set once the draft exists so a failed re-price can be retried
   *  WITHOUT creating a second (orphan) draft. */
  private draftId: string | null = null;

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
      if (this.draftId === null) {
        const created = await firstValueFrom(this.http.post<BookingDto>('/api/v1/bookings', {
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
        this.draftId = created.id;
      }
      const priced = await firstValueFrom(
        this.http.post<RePriceResponse>(`/api/v1/bookings/${this.draftId}/re-price`, {}),
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
