import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService, ThemeDto } from './api.service';

@Component({
  selector: 'app-theme-marketplace-page',
  standalone: true,
  template: `
    <h2>Theme marketplace</h2>
    <p class="muted">Start from a curated preset — cloning makes a private draft you can tune in the studio.</p>
    @if (error()) { <p class="error">{{ error() }}</p> }

    @if (loading()) {
      <p class="muted">Loading…</p>
    } @else {
      <div class="gallery">
        @for (p of presets(); track p.id) {
          <div class="preset" [style.--accent]="token(p, 'foundation', 'accent')"
               [style.--bg]="token(p, 'semantic', 'bg')"
               [style.--surface]="token(p, 'semantic', 'surface')"
               [style.--price]="token(p, 'semantic', 'price')">
            <div class="swatch">
              <div class="bar"></div>
              <div class="card">
                <div class="img"></div>
                <div class="meta">
                  <span class="dot"></span>
                  <span class="line"></span>
                </div>
                <div class="price">€799</div>
              </div>
            </div>
            <div class="foot">
              <div class="name">{{ p.name }}</div>
              <button type="button" (click)="use(p)" [disabled]="cloning() === p.id">
                {{ cloning() === p.id ? 'Adding…' : 'Use this preset' }}
              </button>
            </div>
          </div>
        } @empty {
          <p class="muted">No presets available.</p>
        }
      </div>
    }
  `,
  styles: [`
    .gallery { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 18px; }
    .preset { border: 1px solid #e3e3e3; border-radius: 12px; overflow: hidden; background: #fff; }
    .swatch { padding: 16px; background: var(--bg, #f5f5f5); }
    .bar { height: 8px; border-radius: 999px; background: var(--accent, #888); margin-bottom: 12px; width: 60%; }
    .card { background: var(--surface, #fff); border-radius: 10px; padding: 10px; box-shadow: 0 1px 4px rgba(0,0,0,.08); }
    .img { height: 70px; border-radius: 6px; background: linear-gradient(120deg, var(--accent, #888), color-mix(in srgb, var(--accent, #888) 50%, #fff)); }
    .meta { display: flex; align-items: center; gap: 8px; margin: 10px 0 6px; }
    .dot { width: 14px; height: 14px; border-radius: 50%; background: var(--accent, #888); }
    .line { height: 8px; border-radius: 999px; background: #e2e2e2; flex: 1; }
    .price { font-weight: 700; color: var(--price, #111); }
    .foot { display: flex; align-items: center; justify-content: space-between; padding: 12px 14px; gap: 8px; }
    .name { font-weight: 600; }
    button { padding: 7px 12px; border: 0; border-radius: 6px; background: #0a2540; color: #fff; cursor: pointer; font-size: 0.85rem; }
    button:disabled { opacity: 0.5; cursor: default; }
    .muted { color: #888; }
    .error { color: #c00; }
  `],
})
export class ThemeMarketplacePage {
  private api = inject(ApiService);
  private router = inject(Router);

  presets = signal<ThemeDto[]>([]);
  loading = signal(true);
  cloning = signal<string | null>(null);
  error = signal<string | null>(null);

  constructor() {
    this.api.listPresets().subscribe({
      next: (xs) => { this.presets.set(xs); this.loading.set(false); },
      error: (e) => { this.error.set(e?.error?.detail ?? 'Failed to load presets'); this.loading.set(false); },
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
}
