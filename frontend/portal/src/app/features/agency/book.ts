import { HttpClient } from '@angular/common/http';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { BookingDto, PassengerDto, RePriceResponse } from '../../core/api.types';
import { problemMessage } from '../../core/problem';
import { OdDatePipe } from '../../shared/pipes/date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import {
  Banner, ButtonDirective, Card, Deadline, DialogService, EmptyState, Field, Icon, InputDirective,
  PageHeader, Skeleton, StatusChip,
} from '../../shared/ui';
import { BookingDraftService, DraftContext } from './booking-draft.service';
import {
  PassengerForm, emptyPassenger, passengerTypeAt, toPassengerRequest, validatePassengers,
} from './passenger-form';

type Step = 'passengers' | 'price' | 'submit' | 'done';

const STEPS: { key: Step; label: string; title: string }[] = [
  { key: 'passengers', label: 'Travellers', title: 'Who’s travelling?' },
  { key: 'price', label: 'Price', title: 'Review the price' },
  { key: 'submit', label: 'Submit', title: 'Ready to submit' },
  { key: 'done', label: 'Result', title: 'Booking submitted' },
];

/** A saved draft comes back as a booking; the wizard needs it in the shape search produced. */
export function contextFromBooking(b: BookingDto): DraftContext {
  return {
    programId: '',
    programName: b.programName,
    departureId: '',
    checkIn: b.checkIn,
    checkOut: b.checkOut,
    nights: Math.max(1, Math.round((Date.parse(b.checkOut) - Date.parse(b.checkIn)) / 86_400_000)),
    adults: b.adults,
    children: b.children,
    offer: {
      hotelId: '', hotelName: b.hotelName, stars: 0, roomType: '', board: '',
      sellPrice: b.sellPrice ?? 0, currency: b.currency, agencyCommission: b.agencyCommission ?? 0,
      offerToken: '', offerExpiresAt: null,
    },
    searchSellPrice: b.sellPrice ?? 0,
  };
}

export function formFromPassenger(p: PassengerDto): PassengerForm {
  const date = (iso: string | null) => (iso ? iso.slice(0, 10) : '');
  return {
    title: p.title ?? '',
    gender: p.gender === 'Male' || p.gender === 'Female' ? p.gender : '',
    firstName: p.firstName,
    lastName: p.lastName,
    birthDate: date(p.birthDate),
    nationality: p.nationality ?? '',
    documentType: p.documentType === 'Passport' || p.documentType === 'IdCard' ? p.documentType : '',
    documentNumber: p.documentNumber ?? '',
    documentIssuingCountry: p.documentIssuingCountry ?? '',
    documentExpiryDate: date(p.documentExpiryDate),
    email: p.email ?? '',
    phone: p.phone ?? '',
  };
}

/**
 * Agency wizard: travellers → live price → submit → result. Submitting hands the
 * reservation to the supplier and starts the payment clock; the operator confirms
 * once paid. A draft left mid-way can be resumed from the bookings list.
 */
@Component({
  selector: 'app-agency-book',
  imports: [
    FormsModule, RouterLink, MoneyPipe, OdDatePipe,
    Banner, ButtonDirective, Card, Deadline, EmptyState, Field, Icon, InputDirective, PageHeader, Skeleton,
    StatusChip,
  ],
  templateUrl: './book.html',
  styleUrl: './book.scss',
})
export class AgencyBook {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly draft = inject(BookingDraftService);
  private readonly dialogs = inject(DialogService);

  /** Query parameter: resume a server-side draft instead of starting from search. */
  readonly bookingId = input<string>();

  readonly context = signal<DraftContext | null>(this.draft.context());
  readonly loading = signal(false);
  readonly step = signal<Step>('passengers');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly booking = signal<BookingDto | null>(null);
  readonly drifted = signal(false);
  readonly accepted = signal(false);

  readonly steps = STEPS;
  readonly title = computed(() => STEPS.find(s => s.key === this.step())?.title ?? '');
  readonly stepIndex = computed(() => STEPS.findIndex(s => s.key === this.step()));

  /** What the summary shows: the live price once we have it, the search price before. */
  readonly total = computed(() => this.booking()?.sellPrice ?? this.context()?.searchSellPrice ?? null);
  readonly commission = computed(
    () => this.booking()?.agencyCommission ?? this.context()?.offer.agencyCommission ?? null);

  /** Set once the draft exists so later edits replace its passengers instead of
   *  creating a second (orphan) draft. */
  private draftId: string | null = null;

  // Exactly the party the offer was priced for: adults first, then children.
  passengers: PassengerForm[] = [];
  leadIndex = 0;

  constructor() {
    const ctx = this.context();
    if (ctx) this.passengers = Array.from({ length: ctx.adults + ctx.children }, emptyPassenger);

    effect(() => {
      const id = this.bookingId();
      if (id && !this.draftId) void this.resume(id);
    });
  }

  /** "Traveller 2 · Adult", with a warning suffix when the birth date says otherwise. */
  cardTitle(index: number, p: PassengerForm): string {
    const ctx = this.context();
    if (!ctx) return `Traveller ${index + 1}`;
    const slot = index < ctx.adults ? 'Adult' : 'Child';
    const actual = p.birthDate && p.birthDate <= ctx.checkIn ? passengerTypeAt(p.birthDate, ctx.checkIn) : null;
    const mismatch = actual && actual !== slot ? ` · ${actual.toLowerCase()} at check-in` : '';
    return `Traveller ${index + 1} · ${slot}${mismatch}`;
  }

  problems(): string[] {
    const ctx = this.context();
    return ctx ? validatePassengers(this.passengers, this.leadIndex, ctx) : [];
  }

  stars(count: number): number[] {
    return Array.from({ length: count });
  }

  travellerNames(): string {
    return this.passengers
      .map((p, i) => `${p.firstName} ${p.lastName}${i === this.leadIndex ? ' (lead)' : ''}`.trim())
      .join(', ');
  }

  async continueToPrice(): Promise<void> {
    const ctx = this.context();
    if (!ctx || this.problems().length > 0) return;
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
      this.accepted.set(false);
      this.step.set('price');
    });
  }

  continueToSubmit(): void {
    if (this.drifted() && !this.accepted()) return;
    this.step.set('submit');
  }

  back(): void {
    this.step.set(this.step() === 'submit' ? 'price' : 'passengers');
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

  private async resume(id: string): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const b = await firstValueFrom(this.http.get<BookingDto>(`/api/v1/bookings/${id}`));
      if (b.status !== 'Draft' && b.status !== 'PriceConfirmed') {
        await this.router.navigate(['/agency/bookings', id]);
        return;
      }
      this.draftId = b.id;
      this.context.set(contextFromBooking(b));
      this.passengers = b.passengers.length > 0
        ? b.passengers.map(formFromPassenger)
        : Array.from({ length: b.adults + b.children }, emptyPassenger);
      this.leadIndex = Math.max(0, b.passengers.findIndex(p => p.isLead));
      this.booking.set(b);
    } catch (err) {
      this.error.set(problemMessage(err, 'Could not load this draft.'));
    } finally {
      this.loading.set(false);
    }
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
