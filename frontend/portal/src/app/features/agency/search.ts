import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import {
  HotelOffer, ProgramDetail, ProgramListItem, SearchResponse,
} from '../../core/api.types';
import { problemMessage } from '../../core/problem';
import { OdDatePipe } from '../../shared/pipes/date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import {
  Banner, ButtonDirective, Card, EmptyState, Icon, PageHeader, Pill, Skeleton,
} from '../../shared/ui';
import { BookingDraftService } from './booking-draft.service';

@Component({
  selector: 'app-agency-search',
  imports: [
    FormsModule, MoneyPipe, OdDatePipe,
    Banner, ButtonDirective, Card, EmptyState, Icon, PageHeader, Pill, Skeleton,
  ],
  templateUrl: './search.html',
  styleUrl: './search.scss',
})
export class AgencySearch {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly draft = inject(BookingDraftService);

  // Signals, not plain fields: canSearch() must recompute when these change.
  readonly programId = signal('');
  readonly departureId = signal('');
  adults = 2;
  children = 0;

  readonly searching = signal(false);
  readonly error = signal<string | null>(null);
  readonly results = signal<SearchResponse | null>(null);

  readonly programs = resource({
    loader: () => firstValueFrom(this.http.get<ProgramListItem[]>('/api/v1/catalog/programs')),
  });

  readonly detail = resource({
    params: () => (this.programId() ? { id: this.programId() } : undefined),
    loader: ({ params }) =>
      firstValueFrom(this.http.get<ProgramDetail>(`/api/v1/catalog/programs/${params.id}`)),
  });

  readonly canSearch = computed(() => this.programId() !== '' && this.departureId() !== '');

  selectProgram(id: string): void {
    this.programId.set(id);
    this.departureId.set('');
    this.results.set(null);
  }

  expired(offer: HotelOffer): boolean {
    return offer.offerExpiresAt !== null && Date.parse(offer.offerExpiresAt) < Date.now();
  }

  stars(count: number): number[] {
    return Array.from({ length: count });
  }

  async search(): Promise<void> {
    if (!this.canSearch()) return;
    this.searching.set(true);
    this.error.set(null);
    this.results.set(null);
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

  book(search: SearchResponse, offer: HotelOffer): void {
    if (this.expired(offer)) {
      this.error.set('This offer has expired — please search again.');
      return;
    }
    this.draft.start(search, offer, this.adults, this.children);
    void this.router.navigate(['/agency/book']);
  }
}
