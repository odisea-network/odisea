import { HttpClient } from '@angular/common/http';
import { Component, ElementRef, computed, effect, inject, resource, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import {
  HotelOffer, ProgramDetail, ProgramListItem, SearchResponse,
} from '../../core/api.types';
import { problemMessage } from '../../core/problem';
import { ViewportService } from '../../core/viewport';
import { OdDatePipe } from '../../shared/pipes/date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import {
  Banner, ButtonDirective, Card, EmptyState, Icon, InputDirective, PageHeader, Pill, SegmentOption, Segmented,
  Skeleton,
} from '../../shared/ui';
import { BookingDraftService } from './booking-draft.service';
import { OfferCard } from './offer-card';

type View = 'cards' | 'table';
type Sort = 'price-asc' | 'price-desc' | 'stars' | 'commission';

const MAX_COMPARE = 3;

export function sortOffers(offers: HotelOffer[], sort: Sort): HotelOffer[] {
  const sorted = [...offers];
  switch (sort) {
    case 'price-asc': return sorted.sort((a, b) => a.sellPrice - b.sellPrice);
    case 'price-desc': return sorted.sort((a, b) => b.sellPrice - a.sellPrice);
    case 'stars': return sorted.sort((a, b) => b.stars - a.stars || a.sellPrice - b.sellPrice);
    case 'commission': return sorted.sort((a, b) => b.agencyCommission - a.agencyCommission);
  }
}

@Component({
  selector: 'app-agency-search',
  imports: [
    FormsModule, MoneyPipe, OdDatePipe, OfferCard,
    Banner, ButtonDirective, Card, EmptyState, Icon, InputDirective, PageHeader, Pill, Segmented, Skeleton,
  ],
  templateUrl: './search.html',
  styleUrl: './search.scss',
})
export class AgencySearch {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly draft = inject(BookingDraftService);
  private readonly viewport = inject(ViewportService);

  // Signals, not plain fields: canSearch() must recompute when these change.
  readonly programId = signal('');
  readonly departureId = signal('');
  adults = 2;
  children = 0;

  readonly searching = signal(false);
  readonly error = signal<string | null>(null);
  readonly results = signal<SearchResponse | null>(null);

  readonly view = signal<View>('cards');
  readonly sort = signal<Sort>('price-asc');
  /** Offer tokens picked for side-by-side comparison, at most three. */
  readonly compare = signal<string[]>([]);
  readonly compareOpen = signal(false);

  readonly views: SegmentOption[] = [
    { value: 'cards', label: 'Cards' },
    { value: 'table', label: 'Table' },
  ];

  private readonly compareDialog = viewChild<ElementRef<HTMLDialogElement>>('compareDialog');

  readonly programs = resource({
    loader: () => firstValueFrom(this.http.get<ProgramListItem[]>('/api/v1/catalog/programs')),
  });

  readonly detail = resource({
    params: () => (this.programId() ? { id: this.programId() } : undefined),
    loader: ({ params }) =>
      firstValueFrom(this.http.get<ProgramDetail>(`/api/v1/catalog/programs/${params.id}`)),
  });

  readonly canSearch = computed(() => this.programId() !== '' && this.departureId() !== '');
  readonly phone = this.viewport.isPhone;
  readonly showCards = computed(() => this.view() === 'cards' || this.phone());

  readonly offers = computed(() => sortOffers(this.results()?.offers ?? [], this.sort()));
  readonly compared = computed(() => {
    const picked = this.compare();
    return this.offers().filter(o => picked.includes(o.offerToken));
  });
  readonly compareFull = computed(() => this.compare().length >= MAX_COMPARE);

  constructor() {
    effect(() => {
      const dialog = this.compareDialog()?.nativeElement;
      if (!dialog) return;
      if (this.compareOpen() && !dialog.open) dialog.showModal();
      else if (!this.compareOpen() && dialog.open) dialog.close();
    });
  }

  selectProgram(id: string): void {
    this.programId.set(id);
    this.departureId.set('');
    this.results.set(null);
    this.compare.set([]);
  }

  expired(offer: HotelOffer): boolean {
    return offer.offerExpiresAt !== null && Date.parse(offer.offerExpiresAt) < Date.now();
  }

  stars(count: number): number[] {
    return Array.from({ length: count });
  }

  isCompared(offer: HotelOffer): boolean {
    return this.compare().includes(offer.offerToken);
  }

  toggleCompare(offer: HotelOffer): void {
    const current = this.compare();
    if (current.includes(offer.offerToken)) {
      this.compare.set(current.filter(t => t !== offer.offerToken));
    } else if (current.length < MAX_COMPARE) {
      this.compare.set([...current, offer.offerToken]);
    }
  }

  clearCompare(): void {
    this.compare.set([]);
    this.compareOpen.set(false);
  }

  setView(value: string): void {
    this.view.set(value as View);
  }

  setSort(value: string): void {
    this.sort.set(value as Sort);
  }

  async search(): Promise<void> {
    if (!this.canSearch()) return;
    this.searching.set(true);
    this.error.set(null);
    this.results.set(null);
    this.compare.set([]);
    try {
      this.results.set(await firstValueFrom(this.http.post<SearchResponse>('/api/v1/search', {
        programId: this.programId(),
        departureId: this.departureId(),
        adults: this.adults,
        children: this.children,
      })));
    } catch (err) {
      this.error.set(problemMessage(err, 'Search failed.'));
    } finally {
      this.searching.set(false);
    }
  }

  book(offer: HotelOffer): void {
    const search = this.results();
    if (!search) return;
    if (this.expired(offer)) {
      this.error.set('This offer has expired — please search again.');
      return;
    }
    this.compareOpen.set(false);
    this.draft.start(search, offer, this.adults, this.children);
    void this.router.navigate(['/agency/book']);
  }
}
