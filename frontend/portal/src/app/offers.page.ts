import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiService, OfferDto } from './api.service';

@Component({
  selector: 'app-offers-page',
  standalone: true,
  imports: [CommonModule],
  template: `
    <h2>Offers</h2>
    <p class="muted" *ngIf="loading()">Loading…</p>
    <p class="error" *ngIf="error()">{{ error() }}</p>
    <table *ngIf="!loading() && !error()">
      <thead>
        <tr>
          <th>Title</th>
          <th>Country</th>
          <th>City</th>
          <th>Nights</th>
          <th>Board</th>
          <th>Transport</th>
          <th>Price</th>
          <th>Visibility</th>
        </tr>
      </thead>
      <tbody>
        <tr *ngFor="let o of offers()">
          <td>{{ o.title }}</td>
          <td>{{ o.country }}</td>
          <td>{{ o.city }}</td>
          <td>{{ o.durationNights }}</td>
          <td>{{ o.boardBasis }}</td>
          <td>{{ o.transport }}</td>
          <td>{{ o.price | number:'1.0-2' }} {{ o.currency }}</td>
          <td><small>{{ o.visibility }}</small></td>
        </tr>
      </tbody>
    </table>
  `,
  styles: [`
    table { width: 100%; border-collapse: collapse; }
    th, td { padding: 8px 10px; border-bottom: 1px solid #eee; text-align: left; }
    th { font-weight: 600; background: #fafafa; }
    .muted { color: #888; }
    .error { color: #c00; }
  `],
})
export class OffersPage {
  private api = inject(ApiService);
  offers = signal<OfferDto[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);

  constructor() {
    this.api.listOffers().subscribe({
      next: (xs) => { this.offers.set(xs); this.loading.set(false); },
      error: (e) => { this.error.set(e.message ?? 'Failed to load'); this.loading.set(false); },
    });
  }
}
