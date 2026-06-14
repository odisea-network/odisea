import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService, CollectionDto } from './api.service';
import { forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { OdIcon } from './shared/od-icon';

interface Row { col: CollectionDto; offerCount: number; }

const STATUS_META: Record<string, { tone: string; label: string }> = {
  Published: { tone: 'success', label: 'Публикувана' },
  Draft: { tone: 'warning', label: 'Чернова' },
};

@Component({
  selector: 'app-collections-page',
  standalone: true,
  imports: [RouterLink, OdIcon],
  template: `
    <div class="od-page">
      <div class="od-pagehead">
        <div>
          <div class="od-eyebrow">Дистрибуция</div>
          <h1>Колекции</h1>
          <p class="od-pagehead__sub">Параметризирани селекции от каталога, които остават актуални. Една колекция захранва решетка, въртележка или търсачка във вашите сайтове.</p>
        </div>
        <div class="od-pagehead__actions">
          <a routerLink="/builder" class="od-btn od-btn--primary"><od-icon name="plus" [size]="16" />Нова публикация</a>
        </div>
      </div>

      @if (error()) { <div class="od-notice od-notice--error" style="margin-top:18px"><od-icon name="alert" [size]="16" /><span>{{ error() }}</span></div> }

      @if (loading()) {
        <p class="muted" style="margin-top:20px">Зареждане…</p>
      } @else if (rows().length === 0) {
        <div class="od-empty" style="margin-top:20px">
          <div class="od-empty__icon"><od-icon name="layers" [size]="22" /></div>
          <h3>Все още нямате колекции</h3>
          <p>Създайте колекция от каталога и я публикувайте като вграден компонент.</p>
          <a routerLink="/builder" class="od-btn od-btn--primary"><od-icon name="plus" [size]="16" />Нова публикация</a>
        </div>
      } @else {
        <div class="od-tablewrap" style="margin-top:20px">
          <table class="od-table">
            <thead><tr><th>Колекция</th><th>Адрес (slug)</th><th>Статус</th><th style="text-align:right">Оферти</th></tr></thead>
            <tbody>
              @for (r of rows(); track r.col.id) {
                <tr>
                  <td>
                    <div class="col">
                      <div class="col__icon"><od-icon name="layers" [size]="16" /></div>
                      <span class="col__name">{{ r.col.name }}</span>
                    </div>
                  </td>
                  <td><code class="od-mono slug">{{ r.col.slug }}</code></td>
                  <td><span class="od-badge od-badge--sm" [class]="'od-badge--' + meta(r.col.status).tone">{{ meta(r.col.status).label }}</span></td>
                  <td class="od-num" style="text-align:right;font-weight:700;color:var(--od-ink)">{{ r.offerCount }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
  styles: [`
    .muted { color: var(--od-500); }
    .col { display: flex; align-items: center; gap: 11px; }
    .col__icon { width: 34px; height: 34px; border-radius: 9px; background: var(--od-teal-50); color: var(--od-teal-600); display: grid; place-items: center; flex: none; }
    .col__name { font-weight: 600; color: var(--od-ink); }
    .slug { background: var(--od-100); padding: 2px 8px; border-radius: 5px; font-size: 12.5px; color: var(--od-700); }
  `],
})
export class CollectionsPage {
  private api = inject(ApiService);
  rows = signal<Row[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);

  constructor() {
    this.api.listCollections().subscribe({
      next: (cols) => {
        if (cols.length === 0) { this.rows.set([]); this.loading.set(false); return; }
        forkJoin(
          cols.map(c =>
            this.api.resolveCollection(c.slug).pipe(
              map(offers => ({ col: c, offerCount: offers.length } as Row)),
              catchError(() => of({ col: c, offerCount: 0 } as Row)),
            )
          )
        ).subscribe(rs => { this.rows.set(rs); this.loading.set(false); });
      },
      error: (e) => { this.error.set(e?.error?.detail ?? e.message ?? 'Failed to load'); this.loading.set(false); },
    });
  }

  meta(s: string): { tone: string; label: string } { return STATUS_META[s] ?? { tone: 'neutral', label: s }; }
}
