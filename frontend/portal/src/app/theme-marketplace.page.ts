import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService, ThemeDto } from './api.service';
import { OdIcon } from './shared/od-icon';

@Component({
  selector: 'app-theme-marketplace-page',
  standalone: true,
  imports: [OdIcon],
  template: `
    <div class="od-page">
      <div class="od-pagehead">
        <div>
          <div class="od-eyebrow">Дизайн система</div>
          <h1>Пазар на теми</h1>
          <p class="od-pagehead__sub">Разгледайте готови теми и вашите собствени. Клонирайте предварителна тема в нова чернова и я довършете в редактора.</p>
        </div>
      </div>

      @if (error()) { <div class="od-notice od-notice--error" style="margin-top:18px"><od-icon name="alert" [size]="16" /><span>{{ error() }}</span></div> }

      <section style="margin-top:26px">
        <div class="block-head"><h2 class="block-title">Готови теми</h2><span class="muted">{{ presets().length }} предложения</span></div>
        @if (loading()) {
          <p class="muted">Зареждане…</p>
        } @else {
          <div class="gallery">
            @for (p of presets(); track p.id) {
              <div class="preset"
                   [style.--accent]="token(p, 'foundation', 'accent') || '#1a5a61'"
                   [style.--bg]="token(p, 'semantic', 'bg') || 'var(--od-50)'"
                   [style.--surface]="token(p, 'semantic', 'surface') || '#fff'"
                   [style.--price]="token(p, 'semantic', 'price') || '#0e1618'">
                <div class="preview">
                  <div class="pv-card">
                    <div class="pv-img"></div>
                    <div class="pv-body">
                      <div class="pv-line" style="width:80%"></div>
                      <div class="pv-line" style="width:55%"></div>
                      <div class="pv-foot"><span class="pv-price">€799</span><span class="pv-cta">Виж</span></div>
                    </div>
                  </div>
                </div>
                <div class="preset__foot">
                  <div class="preset__row">
                    <div>
                      <div class="preset__name">{{ p.name }}</div>
                      <div class="muted" style="font-size:12px">Готова тема</div>
                    </div>
                    <div class="swatches">
                      <span [style.background]="token(p, 'foundation', 'accent') || '#1a5a61'"></span>
                      <span [style.background]="token(p, 'semantic', 'surface') || '#fff'"></span>
                      <span [style.background]="token(p, 'semantic', 'price') || '#0e1618'"></span>
                    </div>
                  </div>
                  <button type="button" class="od-btn od-btn--secondary od-btn--sm od-btn--full" style="margin-top:13px" [disabled]="cloning() === p.id" (click)="use(p)">
                    <od-icon name="copy" [size]="15" />{{ cloning() === p.id ? 'Добавяне…' : 'Клонирай като основа' }}
                  </button>
                </div>
              </div>
            } @empty { <p class="muted">Няма налични теми.</p> }
          </div>
        }
      </section>

      <section style="margin-top:38px">
        <div class="block-head"><h2 class="block-title">Моите теми</h2><span class="muted">{{ myThemes().length }} теми</span></div>
        @if (myThemes().length === 0) {
          <div class="od-empty">
            <div class="od-empty__icon"><od-icon name="sliders" [size]="22" /></div>
            <h3>Все още нямате теми</h3>
            <p>Клонирайте готова тема по-горе, за да създадете първата си чернова.</p>
          </div>
        } @else {
          <div class="od-tablewrap">
            @for (th of myThemes(); track th.id; let i = $index) {
              <div class="theme" [class.divider]="i > 0">
                <div class="theme__icon"><od-icon name="sliders" [size]="17" /></div>
                <div class="theme__main">
                  <div class="theme__top">
                    <span class="theme__name">{{ th.name }}</span>
                    <span class="od-badge od-badge--sm" [class]="th.status === 'Published' ? 'od-badge--success' : 'od-badge--warning'">{{ th.status === 'Published' ? 'Публикувана' : 'Чернова' }}</span>
                  </div>
                  <div class="muted" style="font-size:12px;margin-top:2px">Версия {{ th.version }}</div>
                </div>
                <button type="button" class="od-btn od-btn--secondary od-btn--sm" (click)="edit(th)"><od-icon name="settings" [size]="15" />Редактирай</button>
              </div>
            }
          </div>
        }
      </section>
    </div>
  `,
  styles: [`
    .muted { color: var(--od-500); }
    .block-head { display: flex; align-items: baseline; justify-content: space-between; margin-bottom: 14px; }
    .block-title { margin: 0; font-size: 17px; font-weight: 700; color: var(--od-ink); }
    .gallery { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 18px; }
    .preset { background: var(--od-surface); border: 1px solid var(--od-border); border-radius: var(--od-r-lg); overflow: hidden; box-shadow: var(--od-shadow-xs); display: flex; flex-direction: column; }
    .preview { padding: 16px; background: var(--bg); }
    .pv-card { background: var(--surface); border-radius: 12px; padding: 10px; box-shadow: 0 1px 6px rgba(0,0,0,.08); }
    .pv-img { height: 84px; border-radius: 8px; background: linear-gradient(120deg, var(--accent), color-mix(in srgb, var(--accent) 45%, #fff)); }
    .pv-line { height: 8px; border-radius: 999px; background: #e2e2e2; margin: 10px 0 0; }
    .pv-foot { display: flex; align-items: center; justify-content: space-between; margin-top: 12px; }
    .pv-price { font-weight: 800; font-size: 15px; color: var(--price); }
    .pv-cta { font-size: 11px; font-weight: 700; color: #fff; background: var(--accent); border-radius: 999px; padding: 4px 10px; }
    .preset__foot { padding: 14px 16px; border-top: 1px solid var(--od-border); }
    .preset__row { display: flex; align-items: center; justify-content: space-between; gap: 10px; }
    .preset__name { font-size: 14.5px; font-weight: 700; color: var(--od-ink); }
    .swatches { display: flex; gap: 5px; }
    .swatches span { width: 18px; height: 18px; border-radius: 5px; border: 1px solid rgba(0,0,0,0.12); }
    .theme { display: flex; align-items: center; gap: 14px; padding: 14px 18px; }
    .theme.divider { border-top: 1px solid var(--od-border-2); }
    .theme__icon { width: 36px; height: 36px; border-radius: 9px; background: var(--od-teal-50); color: var(--od-teal-600); display: grid; place-items: center; flex: none; }
    .theme__main { flex: 1; min-width: 0; }
    .theme__top { display: flex; align-items: center; gap: 8px; }
    .theme__name { font-size: 14px; font-weight: 700; color: var(--od-ink); }
  `],
})
export class ThemeMarketplacePage {
  private api = inject(ApiService);
  private router = inject(Router);

  presets = signal<ThemeDto[]>([]);
  myThemes = signal<ThemeDto[]>([]);
  loading = signal(true);
  cloning = signal<string | null>(null);
  error = signal<string | null>(null);

  constructor() {
    this.api.listPresets().subscribe({
      next: (xs) => { this.presets.set(xs); this.loading.set(false); },
      error: (e) => { this.error.set(e?.error?.detail ?? 'Failed to load presets'); this.loading.set(false); },
    });
    this.api.listThemes().subscribe({
      next: (xs) => this.myThemes.set(xs.filter(t => !t.isPreset)),
      error: () => { /* presets are the primary content; my-themes failure is non-fatal */ },
    });
  }

  token(p: ThemeDto, group: 'foundation' | 'semantic' | 'component', key: string): string {
    return p.tokens?.[group]?.[key] ?? '';
  }

  use(p: ThemeDto): void {
    this.cloning.set(p.id);
    this.error.set(null);
    this.api.cloneFromPreset(p.id).subscribe({
      next: (theme) => this.router.navigate(['/themes', theme.id]),
      error: (e) => { this.error.set(e?.error?.detail ?? 'Clone failed'); this.cloning.set(null); },
    });
  }

  edit(t: ThemeDto): void {
    this.router.navigate(['/themes', t.id]);
  }
}
