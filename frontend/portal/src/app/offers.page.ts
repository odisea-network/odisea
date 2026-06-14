import { Component, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService, OfferDto } from './api.service';
import { OdIcon } from './shared/od-icon';

const STATUS_META: Record<string, { tone: string; label: string }> = {
  Published: { tone: 'success', label: 'Публикувана' },
  Draft: { tone: 'warning', label: 'Чернова' },
};

@Component({
  selector: 'app-offers-page',
  standalone: true,
  imports: [DecimalPipe, FormsModule, OdIcon],
  template: `
    <div class="od-page">
      <div class="od-pagehead">
        <div>
          <div class="od-eyebrow">Споделен каталог</div>
          <h1>Оферти</h1>
          <p class="od-pagehead__sub">Публикувайте веднъж, дистрибуцията се случва. Всяка оферта става достъпна за вашите колекции и партньорски сайтове.</p>
        </div>
      </div>

      <div class="od-stats" style="margin-top:22px;margin-bottom:22px">
        <div class="od-stat"><div class="od-stat__top"><span class="od-stat__label">Общо оферти</span><span class="od-stat__icon"><od-icon name="package" [size]="16" /></span></div><div class="od-stat__value">{{ offers().length }}</div></div>
        <div class="od-stat"><div class="od-stat__top"><span class="od-stat__label">Публикувани</span><span class="od-stat__icon"><od-icon name="check" [size]="16" /></span></div><div class="od-stat__value">{{ countStatus('Published') }}</div></div>
        <div class="od-stat"><div class="od-stat__top"><span class="od-stat__label">Чернови</span><span class="od-stat__icon gold"><od-icon name="layers" [size]="16" /></span></div><div class="od-stat__value">{{ countStatus('Draft') }}</div></div>
        <div class="od-stat"><div class="od-stat__top"><span class="od-stat__label">Държави</span><span class="od-stat__icon"><od-icon name="pin" [size]="16" /></span></div><div class="od-stat__value">{{ countries() }}</div></div>
      </div>

      @if (error()) { <div class="od-notice od-notice--error"><od-icon name="alert" [size]="16" /><span>{{ error() }}</span></div> }

      <div class="bar">
        <div class="od-input od-input--withicon" style="width:260px"><od-icon name="search" [size]="15" /><input [ngModel]="q()" (ngModelChange)="q.set($event)" placeholder="Търси оферта…" /></div>
        <span class="muted">{{ filtered().length }} от {{ offers().length }}</span>
      </div>

      @if (loading()) {
        <p class="muted">Зареждане…</p>
      } @else {
        <div class="od-tablewrap">
          <table class="od-table">
            <thead><tr>
              <th>Оферта</th><th>Дестинация</th><th>Изхранване</th><th style="text-align:right">Нощувки</th><th style="text-align:right">Цена от</th><th>Статус</th>
            </tr></thead>
            <tbody>
              @for (o of filtered(); track o.id) {
                <tr>
                  <td>
                    <div class="offer">
                      @if (o.imageUrl) { <img [src]="o.imageUrl" alt="" /> } @else { <div class="offer__ph"></div> }
                      <span class="offer__title">{{ o.title }}</span>
                    </div>
                  </td>
                  <td style="color:var(--od-700)">{{ o.city ? o.city + ', ' : '' }}{{ o.country }}</td>
                  <td style="color:var(--od-700)">{{ o.boardBasis }}</td>
                  <td class="od-num" style="text-align:right">{{ o.durationNights }}</td>
                  <td class="od-num" style="text-align:right;font-weight:700;color:var(--od-ink)">€{{ o.price | number:'1.0-0' }}</td>
                  <td>@if (o.status) { <span class="od-badge od-badge--sm" [class]="'od-badge--' + meta(o.status).tone">{{ meta(o.status).label }}</span> }</td>
                </tr>
              } @empty { <tr><td colspan="6" class="muted">Няма оферти.</td></tr> }
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
  styles: [`
    .muted { color: var(--od-500); }
    .bar { display: flex; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 14px; flex-wrap: wrap; }
    .offer { display: flex; align-items: center; gap: 11px; }
    .offer img, .offer__ph { width: 46px; height: 36px; object-fit: cover; border-radius: 7px; flex: none; background: var(--od-teal-50); }
    .offer__title { font-weight: 600; color: var(--od-ink); }
  `],
})
export class OffersPage {
  private api = inject(ApiService);
  offers = signal<OfferDto[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);
  q = signal('');

  filtered = computed(() => {
    const term = this.q().toLowerCase().trim();
    if (!term) return this.offers();
    return this.offers().filter(o =>
      o.title.toLowerCase().includes(term) || (o.city ?? '').toLowerCase().includes(term) || o.country.toLowerCase().includes(term));
  });

  constructor() {
    this.api.listOffers().subscribe({
      next: (xs) => { this.offers.set(xs); this.loading.set(false); },
      error: (e) => { this.error.set(e?.error?.detail ?? e.message ?? 'Failed to load'); this.loading.set(false); },
    });
  }

  meta(s: string): { tone: string; label: string } { return STATUS_META[s] ?? { tone: 'neutral', label: s }; }
  countStatus(s: string): number { return this.offers().filter(o => o.status === s).length; }
  countries(): number { return new Set(this.offers().map(o => o.country)).size; }
}
