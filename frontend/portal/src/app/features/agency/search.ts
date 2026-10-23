import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import {
  HotelOffer, ProgramDetail, ProgramListItem, SearchResponse,
} from '../../core/api.types';
import { formatDate, formatMoney } from '../../core/format';
import { problemMessage } from '../../core/problem';
import { BookingDraftService } from './booking-draft.service';

@Component({
  selector: 'app-agency-search',
  imports: [FormsModule],
  templateUrl: './search.html',
  styles: `
    .card { background: var(--surface-1); border: 1px solid var(--border); border-radius: .75rem; }
    .criteria { display: flex; flex-wrap: wrap; gap: 1.25rem; align-items: end; padding: 1.25rem; margin-bottom: 1.5rem; }
    .field { display: flex; flex-direction: column; gap: .3rem; }
    .field.pax { flex-direction: row; align-items: center; gap: .5rem; }
    .field.pax input { width: 4rem; }
    label { font-size: .8rem; font-weight: 600; }
    select { padding: .5rem; border: 1px solid var(--border); border-radius: .45rem; background: var(--surface-1); font: inherit; }
    table { width: 100%; border-collapse: collapse; }
    th, td { text-align: left; padding: .7rem 1rem; border-bottom: 1px solid var(--border); }
    th.num, td.num { text-align: right; }
    .strong { font-weight: 700; }
    .muted { color: var(--text-muted); }
    .error { color: var(--danger); }
  `,
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

  readonly fmtMoney = formatMoney;
  readonly fmtDate = formatDate;

  selectProgram(id: string): void {
    this.programId.set(id);
    this.departureId.set('');
    this.results.set(null);
  }

  expired(offer: HotelOffer): boolean {
    return offer.offerExpiresAt !== null && Date.parse(offer.offerExpiresAt) < Date.now();
  }

  async search(): Promise<void> {
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
    this.draft.start(search, offer);
    void this.router.navigate(['/agency/book']);
  }
}
