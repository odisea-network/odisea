import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { BookingDto, RePriceResponse } from '../../core/api.types';
import { problemMessage } from '../../core/problem';
import { OdDatePipe } from '../../shared/pipes/date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import {
  Banner, ButtonDirective, Card, Deadline, DialogService, EmptyState, Field, Icon, InputDirective,
  PageHeader, StatusChip,
} from '../../shared/ui';
import { BookingDraftService } from './booking-draft.service';
import {
  PassengerForm, emptyPassenger, passengerTypeAt, toPassengerRequest, validatePassengers,
} from './passenger-form';

type Step = 'passengers' | 'price' | 'done';

const STEPS: { key: Step; label: string; title: string }[] = [
  { key: 'passengers', label: 'Travellers', title: 'Who’s travelling?' },
  { key: 'price', label: 'Price', title: 'Review the price' },
  { key: 'done', label: 'Submitted', title: 'Booking submitted' },
];

/**
 * Agency wizard: passengers → live price → submit. Submitting hands the reservation
 * to the supplier and starts the payment clock; the operator confirms once paid.
 */
@Component({
  selector: 'app-agency-book',
  imports: [
    FormsModule, RouterLink, MoneyPipe, OdDatePipe,
    Banner, ButtonDirective, Card, Deadline, EmptyState, Field, Icon, InputDirective, PageHeader, StatusChip,
  ],
  templateUrl: './book.html',
  styleUrl: './book.scss',
})
export class AgencyBook {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly draft = inject(BookingDraftService);
  private readonly dialogs = inject(DialogService);

  readonly context = this.draft.context();
  readonly step = signal<Step>('passengers');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly booking = signal<BookingDto | null>(null);
  readonly drifted = signal(false);

  readonly steps = STEPS;
  readonly title = computed(() => STEPS.find(s => s.key === this.step())?.title ?? '');
  readonly stepIndex = computed(() => STEPS.findIndex(s => s.key === this.step()));

  /** What the summary shows: the live price once we have it, the search price before. */
  readonly total = computed(() => this.booking()?.sellPrice ?? this.context?.searchSellPrice ?? null);
  readonly commission = computed(
    () => this.booking()?.agencyCommission ?? this.context?.offer.agencyCommission ?? null);

  /** Set once the draft exists so later edits replace its passengers instead of
   *  creating a second (orphan) draft. */
  private draftId: string | null = null;

  // Exactly the party the offer was priced for: adults first, then children.
  passengers: PassengerForm[] = Array.from(
    { length: (this.context?.adults ?? 1) + (this.context?.children ?? 0) }, emptyPassenger);
  leadIndex = 0;

  slotLabel(index: number): string {
    if (!this.context) return `Traveller ${index + 1}`;
    return index < this.context.adults
      ? `Traveller ${index + 1} · Adult`
      : `Traveller ${index + 1} · Child`;
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

  stars(count: number): number[] {
    return Array.from({ length: count });
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
    const decision = await this.dialogs.confirm({
      title: 'Discard this reservation?',
      message: 'The draft is cancelled. Nothing has been sent to the supplier or charged.',
      confirmLabel: 'Discard reservation',
      cancelLabel: 'Keep editing',
      destructive: true,
    });
    if (!decision) return;
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
