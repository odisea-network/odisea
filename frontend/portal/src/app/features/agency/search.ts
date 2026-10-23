import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import {
  HotelOffer, ProblemDetails, ProgramDetail, ProgramListItem, SearchResponse,
} from '../../core/api.types';
import { formatDate, formatMoney } from '../../core/format';
import { BookingDraftService } from './booking-draft.service';

@Component({
  selector: 'app-agency-search',
  imports: [FormsModule],
  template: `
    <h2>Search packages</h2>

    <section class="criteria card">
      <div class="field">
        <label for="program">Program</label>
        <select id="program" [ngModel]="programId()" (ngModelChange)="selectProgram($event)">
          <option value="" disabled>Choose a program…</option>
          @for (program of programs.value(); track program.id) {
            <option [value]="program.id">{{ program.name }}</option>
          }
        </select>
      </div>

      <div class="field">
        <label for="departure">Departure</label>
        <select id="departure" [(ngModel)]="departureId" [disabled]="!detail.value()">
          <option value="" disabled>Choose dates…</option>
          @for (dep of detail.value()?.departures; track dep.id) {
            <option [value]="dep.id">
              {{ fmtDate(dep.startDate) }} – {{ fmtDate(dep.endDate) }} ({{ dep.nights }} nights)
            </option>
          }
        </select>
      </div>

      <div class="field pax">
        <label for="adults">Adults</label>
        <input id="adults" type="number" min="1" max="9" [(ngModel)]="adults" />
        <label for="children">Children</label>
        <input id="children" type="number" min="0" max="9" [(ngModel)]="children" />
      </div>

      <button type="button" (click)="search()" [disabled]="!canSearch() || searching()">
        {{ searching() ? 'Searching…' : 'Search' }}
      </button>
    </section>

    @if (error()) {
      <p class="error" role="alert">{{ error() }}</p>
    }

    @if (results(); as found) {
      <h3>
        {{ found.programName }} · {{ fmtDate(found.checkIn) }} – {{ fmtDate(found.checkOut) }}
        · {{ found.nights }} nights
      </h3>
      <table class="card">
        <thead>
          <tr>
            <th>Hotel</th><th>Room</th><th>Board</th>
            <th class="num">Price</th><th class="num">Your commission</th><th></th>
          </tr>
        </thead>
        <tbody>
          @for (offer of found.offers; track offer.offerToken) {
            <tr>
              <td>{{ offer.hotelName }} <span class="muted">{{ '★'.repeat(offer.stars) }}</span></td>
              <td>{{ offer.roomType }}</td>
              <td>{{ offer.board }}</td>
              <td class="num strong">{{ fmtMoney(offer.sellPrice, offer.currency) }}</td>
              <td class="num">{{ fmtMoney(offer.agencyCommission, offer.currency) }}</td>
              <td><button type="button" (click)="book(found, offer)">Book</button></td>
            </tr>
          } @empty {
            <tr><td colspan="6">No availability for these criteria.</td></tr>
          }
        </tbody>
      </table>
    }
  `,
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

  readonly programId = signal('');
  departureId = '';
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

  readonly canSearch = computed(() => this.programId() !== '' && this.departureId !== '');

  readonly fmtMoney = formatMoney;
  readonly fmtDate = formatDate;

  selectProgram(id: string): void {
    this.programId.set(id);
    this.departureId = '';
    this.results.set(null);
  }

  async search(): Promise<void> {
    this.searching.set(true);
    this.error.set(null);
    this.results.set(null);
    try {
      this.results.set(await firstValueFrom(this.http.post<SearchResponse>('/api/v1/search', {
        programId: this.programId(),
        departureId: this.departureId,
        adults: this.adults,
        children: this.children,
      })));
    } catch (err) {
      const problem = err instanceof HttpErrorResponse ? (err.error as ProblemDetails) : null;
      this.error.set(problem?.detail ?? 'Search failed.');
    } finally {
      this.searching.set(false);
    }
  }

  book(search: SearchResponse, offer: HotelOffer): void {
    this.draft.start(search, offer);
    void this.router.navigate(['/agency/book']);
  }
}
