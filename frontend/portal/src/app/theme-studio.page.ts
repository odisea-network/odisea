import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService, ThemeDto, ThemeTokens } from './api.service';

const FONT_OPTS   = ['Onest', 'Manrope', 'Inter', 'Georgia', 'Prata'];
const RATIO_OPTS  = ['4 / 3', '16 / 9', '1 / 1'];
const ACCENT_PRESETS = ['#1a5a61', '#C85727', '#1f4ed8', '#1f6b4a', '#7a3ea8', '#0e1618'];

function deepCopy(t: ThemeTokens): ThemeTokens {
  return JSON.parse(JSON.stringify(t));
}

@Component({
  selector: 'app-theme-studio-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <!-- Header -->
    <div class="header">
      <div class="header-left">
        <span class="eyebrow">Дизайн система</span>
        <h1>{{ theme()?.name ?? '…' }}</h1>
        <span class="badge" [class.published]="theme()?.status === 'Published'"
                            [class.draft]="theme()?.status === 'Draft'">
          {{ theme()?.status === 'Published' ? 'Публикувана' : 'Чернова' }}
        </span>
        <span class="version" *ngIf="theme()">v{{ theme()!.version }}</span>
      </div>
      <div class="header-actions">
        <button class="btn-secondary" (click)="saveDraft()" [disabled]="saving()">
          {{ saving() ? 'Запазване…' : 'Запази чернова' }}
        </button>
        <button class="btn-primary" (click)="publish()"
                [disabled]="publishing() || theme()?.status === 'Published'">
          {{ publishing() ? 'Публикуване…' : 'Публикувай' }}
        </button>
      </div>
    </div>

    <p class="error" *ngIf="error()">{{ error() }}</p>
    <p class="muted" *ngIf="loading()">Зареждане…</p>

    <div class="studio" *ngIf="!loading() && tok()">

      <!-- ── Left: token editor ── -->
      <div class="editor-panel">

        <div class="section-label">Foundation</div>

        <div class="token-row">
          <span>Акцент</span>
          <div class="swatches">
            <button *ngFor="let c of accentPresets" class="swatch"
                    [style.background]="c"
                    [class.active]="tok()!.foundation['accent'] === c"
                    (click)="setF('accent', c)"></button>
            <input type="color" [value]="tok()!.foundation['accent']"
                   (input)="setF('accent', $any($event.target).value)" class="color-picker" />
          </div>
        </div>

        <div class="token-row">
          <span>Шрифт · текст</span>
          <select [ngModel]="tok()!.foundation['fontBody']"
                  (ngModelChange)="setF('fontBody', $event)">
            <option *ngFor="let f of fontOpts">{{ f }}</option>
          </select>
        </div>

        <div class="token-row">
          <span>Шрифт · заглавия</span>
          <select [ngModel]="tok()!.foundation['fontHead']"
                  (ngModelChange)="setF('fontHead', $event)">
            <option *ngFor="let f of fontOpts">{{ f }}</option>
          </select>
        </div>

        <div class="token-row">
          <span>Радиус</span>
          <input type="range" min="0" max="28" step="1"
                 [value]="tok()!.foundation['radius']"
                 (input)="setF('radius', $any($event.target).value)" />
          <span class="mono">{{ tok()!.foundation['radius'] }}px</span>
        </div>

        <div class="section-label">Semantic</div>

        <div class="token-row">
          <span>Цвят на цената</span>
          <div class="swatches">
            <button *ngFor="let c of ['#0e1618','#1a5a61','#C85727','#1f6b4a']" class="swatch"
                    [style.background]="c"
                    [class.active]="tok()!.semantic['price'] === c"
                    (click)="setS('price', c)"></button>
            <input type="color" [value]="tok()!.semantic['price']"
                   (input)="setS('price', $any($event.target).value)" class="color-picker" />
          </div>
        </div>

        <div class="token-row">
          <span>Фон</span>
          <div class="swatches">
            <button *ngFor="let c of ['#f7f9f8','#ffffff','#f6f1ea','#f3f4f6']" class="swatch"
                    [style.background]="c" [class.bordered]="true"
                    [class.active]="tok()!.semantic['bg'] === c"
                    (click)="setS('bg', c)"></button>
            <input type="color" [value]="tok()!.semantic['bg']"
                   (input)="setS('bg', $any($event.target).value)" class="color-picker" />
          </div>
        </div>

        <div class="token-row">
          <span>Повърхност</span>
          <div class="swatches">
            <button *ngFor="let c of ['#ffffff','#fffdf9','#f7f9f8']" class="swatch"
                    [style.background]="c" [class.bordered]="true"
                    [class.active]="tok()!.semantic['surface'] === c"
                    (click)="setS('surface', c)"></button>
            <input type="color" [value]="tok()!.semantic['surface']"
                   (input)="setS('surface', $any($event.target).value)" class="color-picker" />
          </div>
        </div>

        <div class="section-label">Component</div>

        <div class="token-row">
          <span>Съотношение снимка</span>
          <div class="segmented">
            <button *ngFor="let r of ratioOpts"
                    [class.active]="tok()!.component['cardRatio'] === r"
                    (click)="setC('cardRatio', r)">
              {{ r === '4 / 3' ? '4:3' : r === '16 / 9' ? '16:9' : '1:1' }}
            </button>
          </div>
        </div>

      </div>

      <!-- ── Right: live preview + export ── -->
      <div class="preview-col">

        <!-- Live preview card -->
        <div class="preview-panel">
          <div class="preview-header">
            <span class="badge teal">Жив преглед</span>
            <span class="hint">Същият компонент, вашите токени</span>
          </div>
          <div class="preview-body" [ngStyle]="previewVars()">
            <div class="offer-cards">
              <div class="offer-card">
                <div class="card-image" [ngStyle]="{'aspect-ratio': tok()!.component['cardRatio']}">
                  <div class="card-image-placeholder"></div>
                </div>
                <div class="card-body">
                  <div class="card-title">Санторини — Кикладска магия</div>
                  <div class="card-sub">Гърция · 7 нощи · All inclusive</div>
                  <div class="card-price">от 1 249 €</div>
                  <button class="card-cta">Виж детайли</button>
                </div>
              </div>
              <div class="offer-card">
                <div class="card-image" [ngStyle]="{'aspect-ratio': tok()!.component['cardRatio']}">
                  <div class="card-image-placeholder alt"></div>
                </div>
                <div class="card-body">
                  <div class="card-title">Малдиви — вила над водата</div>
                  <div class="card-sub">Малдиви · 10 нощи · Ultra all inclusive</div>
                  <div class="card-price">от 3 890 €</div>
                  <button class="card-cta">Виж детайли</button>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Export panel -->
        <div class="export-row">
          <div class="css-preview">
            <div class="code-header">
              <span class="mono-label">theme.css</span>
              <button class="btn-tiny" (click)="copyCss()">
                {{ copied() ? '✓ Копирано' : 'CSS' }}
              </button>
            </div>
            <pre class="code-body">{{ cssPreview() }}</pre>
          </div>
          <div class="export-buttons">
            <div class="export-title">Експорт</div>
            <button class="btn-secondary full" (click)="downloadExport('css')">CSS променливи</button>
            <button class="btn-secondary full" (click)="downloadExport('json')">JSON tokens</button>
            <p class="hint-sm">Custom CSS остава като escape hatch — не като основен интерфейс.</p>
          </div>
        </div>

      </div>
    </div>
  `,
  styles: [`
    :host { display: block; padding: 28px 32px 60px; max-width: 1320px; margin: 0 auto; }

    /* Header */
    .header { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 12px; margin-bottom: 24px; }
    .header-left { display: flex; align-items: center; gap: 10px; }
    .eyebrow { font-size: 11px; font-weight: 700; letter-spacing: .08em; text-transform: uppercase; color: var(--od-500, #6c7976); }
    h1 { margin: 0; font-size: 20px; font-weight: 700; color: var(--od-ink, #0e1618); }
    .version { font-size: 12px; color: var(--od-500, #6c7976); font-family: monospace; }
    .header-actions { display: flex; gap: 8px; }

    /* Badges */
    .badge { display: inline-flex; align-items: center; padding: 3px 8px; border-radius: 99px; font-size: 12px; font-weight: 600; }
    .badge.published { background: #e7f3ec; color: #1c7a4a; }
    .badge.draft     { background: #fbf0d8; color: #9a6a12; }
    .badge.teal      { background: #dcebec; color: #0f3a40; }

    /* Buttons */
    .btn-primary   { padding: 8px 16px; border-radius: 6px; border: none; background: var(--od-teal-600, #1a5a61); color: #fff; font-weight: 600; font-size: 13px; cursor: pointer; }
    .btn-primary:disabled { opacity: .5; cursor: not-allowed; }
    .btn-secondary { padding: 8px 14px; border-radius: 6px; border: 1px solid var(--od-200, #e1e6e4); background: #fff; color: var(--od-ink, #0e1618); font-weight: 600; font-size: 13px; cursor: pointer; }
    .btn-secondary.full { width: 100%; }
    .btn-tiny { padding: 3px 8px; border-radius: 5px; border: 1px solid rgba(255,255,255,.2); background: rgba(255,255,255,.08); color: #fff; font-size: 11.5px; font-weight: 600; cursor: pointer; }

    .error { color: #b3261e; }
    .muted { color: #888; }

    /* Studio layout */
    .studio { display: grid; grid-template-columns: minmax(340px, 420px) 1fr; gap: 20px; align-items: start; }

    /* Editor panel */
    .editor-panel { background: #fff; border: 1px solid var(--od-200, #e1e6e4); border-radius: 12px; padding: 18px 20px; }
    .section-label { font-size: 11px; font-weight: 700; letter-spacing: .08em; text-transform: uppercase; color: var(--od-500, #6c7976); margin: 16px 0 4px; }
    .section-label:first-child { margin-top: 0; }
    .token-row { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 10px 0; border-top: 1px solid var(--od-150, #eaeeec); font-size: 13px; color: var(--od-700, #3a4744); font-weight: 500; }

    /* Swatches */
    .swatches { display: flex; gap: 5px; align-items: center; }
    .swatch { width: 24px; height: 24px; border-radius: 6px; border: 1px solid rgba(0,0,0,.1); cursor: pointer; padding: 0; transition: transform .1s; }
    .swatch.active { border: 2px solid var(--od-ink, #0e1618); transform: scale(1.1); }
    .swatch.bordered { border-color: rgba(0,0,0,.15); }
    .color-picker { width: 26px; height: 26px; border-radius: 6px; border: 1px solid rgba(0,0,0,.1); padding: 0; cursor: pointer; }

    /* Select */
    select { padding: 5px 8px; border-radius: 6px; border: 1px solid var(--od-200, #e1e6e4); font-size: 13px; color: var(--od-ink, #0e1618); background: #fff; cursor: pointer; min-width: 130px; }

    /* Range */
    input[type=range] { width: 130px; accent-color: var(--od-teal-600, #1a5a61); }
    .mono { font-family: monospace; font-size: 12px; color: var(--od-600, #525f5c); width: 36px; text-align: right; }

    /* Segmented */
    .segmented { display: flex; gap: 2px; background: var(--od-100, #f1f4f3); border-radius: 7px; padding: 2px; }
    .segmented button { padding: 4px 10px; border-radius: 5px; border: none; background: transparent; font-size: 12.5px; font-weight: 600; cursor: pointer; color: var(--od-600, #525f5c); }
    .segmented button.active { background: #fff; color: var(--od-ink, #0e1618); box-shadow: 0 1px 3px rgba(0,0,0,.08); }

    /* Preview col */
    .preview-col { display: flex; flex-direction: column; gap: 16px; }

    /* Preview panel */
    .preview-panel { border-radius: 12px; overflow: hidden; border: 1px solid var(--od-200, #e1e6e4); }
    .preview-header { display: flex; align-items: center; justify-content: space-between; padding: 10px 14px; background: #fff; border-bottom: 1px solid var(--od-150, #eaeeec); }
    .hint { font-size: 12px; color: var(--od-500, #6c7976); }
    .preview-body { padding: 24px; }

    /* Offer cards */
    .offer-cards { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; }
    .offer-card { background: var(--preview-surface, #fff); border-radius: calc(var(--preview-radius, 8) * 1px); overflow: hidden; box-shadow: 0 1px 4px rgba(0,0,0,.08); }
    .card-image { width: 100%; overflow: hidden; }
    .card-image-placeholder { width: 100%; height: 100%; background: linear-gradient(135deg, var(--preview-accent, #1a5a61) 0%, color-mix(in srgb, var(--preview-accent, #1a5a61) 60%, white) 100%); min-height: 120px; }
    .card-image-placeholder.alt { background: linear-gradient(135deg, #3f9098 0%, #dcebec 100%); }
    .card-body { padding: 14px; background: var(--preview-surface, #fff); }
    .card-title { font-size: 13.5px; font-weight: 700; color: #0e1618; margin-bottom: 4px; font-family: var(--preview-font-head, inherit); }
    .card-sub { font-size: 12px; color: #6c7976; margin-bottom: 8px; }
    .card-price { font-size: 15px; font-weight: 700; color: var(--preview-price, #0e1618); margin-bottom: 10px; }
    .card-cta { width: 100%; padding: 8px; border-radius: calc(var(--preview-radius, 8) * 1px); border: none; background: var(--preview-accent, #1a5a61); color: #fff; font-size: 13px; font-weight: 600; cursor: pointer; }

    /* Export row */
    .export-row { display: grid; grid-template-columns: 1fr auto; gap: 14px; align-items: start; }
    .css-preview { background: #0e1618; border-radius: 10px; overflow: hidden; }
    .code-header { display: flex; align-items: center; justify-content: space-between; padding: 8px 12px; background: #16211f; }
    .mono-label { font-family: monospace; font-size: 11.5px; color: rgba(255,255,255,.5); }
    .code-body { margin: 0; padding: 12px 14px; font-family: monospace; font-size: 11.5px; line-height: 1.7; color: #cfe8e6; overflow: auto; max-height: 200px; }
    .export-buttons { display: flex; flex-direction: column; gap: 8px; min-width: 170px; padding: 14px 16px; background: #fff; border: 1px solid var(--od-200, #e1e6e4); border-radius: 10px; }
    .export-title { font-size: 13px; font-weight: 700; color: var(--od-ink, #0e1618); }
    .hint-sm { margin: 4px 0 0; font-size: 11.5px; color: var(--od-500, #6c7976); line-height: 1.5; }
  `],
})
export class ThemeStudioPage implements OnInit {
  private route  = inject(ActivatedRoute);
  private router = inject(Router);
  private api    = inject(ApiService);

  theme      = signal<ThemeDto | null>(null);
  tok        = signal<ThemeTokens | null>(null);
  loading    = signal(true);
  saving     = signal(false);
  publishing = signal(false);
  copied     = signal(false);
  error      = signal<string | null>(null);

  readonly accentPresets = ACCENT_PRESETS;
  readonly fontOpts      = FONT_OPTS;
  readonly ratioOpts     = RATIO_OPTS;

  cssPreview = computed(() => {
    const t = this.tok();
    if (!t) return '';
    const lines: string[] = ['.odc-scope {'];
    const push = (key: string, val: string) => {
      const css = key.replace(/([A-Z])/g, '-$1').toLowerCase();
      lines.push(`  --odc-${css}: ${val};`);
    };
    Object.entries(t.foundation).forEach(([k, v]) => push(k, v));
    Object.entries(t.semantic).forEach(([k, v]) => push(k, v));
    Object.entries(t.component).forEach(([k, v]) => push(k, v));
    lines.push('}');
    return lines.join('\n');
  });

  previewVars = computed(() => {
    const t = this.tok();
    if (!t) return {};
    return {
      '--preview-accent':    t.foundation['accent']    ?? '#1a5a61',
      '--preview-font-body': `'${t.foundation['fontBody'] ?? 'Onest'}', sans-serif`,
      '--preview-font-head': `'${t.foundation['fontHead'] ?? 'Onest'}', sans-serif`,
      '--preview-radius':    t.foundation['radius']    ?? '8',
      '--preview-price':     t.semantic['price']       ?? '#0e1618',
      '--preview-bg':        t.semantic['bg']          ?? '#f7f9f8',
      '--preview-surface':   t.semantic['surface']     ?? '#ffffff',
      'background':          t.semantic['bg']          ?? '#f7f9f8',
    };
  });

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id')!;

    // "/themes/new" starts a fresh draft: create it server-side, then swap the
    // URL to the real id so saving, publishing and refresh all work.
    if (id === 'new') {
      // agencyId is taken from the auth token server-side; the empty GUID is a
      // valid placeholder that the API ignores (it must still parse as a Guid).
      this.api.createTheme({ agencyId: '00000000-0000-0000-0000-000000000000', name: 'Нова тема' }).subscribe({
        next: (t) => {
          this.theme.set(t);
          this.tok.set(deepCopy(t.tokens));
          this.loading.set(false);
          this.router.navigate(['/themes', t.id], { replaceUrl: true });
        },
        error: (e) => {
          this.error.set(e?.error?.detail ?? e.message ?? 'Failed to create theme');
          this.loading.set(false);
        },
      });
      return;
    }

    this.api.getTheme(id).subscribe({
      next: (t) => {
        this.theme.set(t);
        this.tok.set(deepCopy(t.tokens));
        this.loading.set(false);
      },
      error: (e) => {
        this.error.set(e?.error?.detail ?? e.message ?? 'Failed to load theme');
        this.loading.set(false);
      },
    });
  }

  setF(key: string, value: string) {
    this.tok.update(t => {
      if (!t) return t;
      const copy = deepCopy(t);
      copy.foundation[key] = value;
      return copy;
    });
  }

  setS(key: string, value: string) {
    this.tok.update(t => {
      if (!t) return t;
      const copy = deepCopy(t);
      copy.semantic[key] = value;
      return copy;
    });
  }

  setC(key: string, value: string) {
    this.tok.update(t => {
      if (!t) return t;
      const copy = deepCopy(t);
      copy.component[key] = value;
      return copy;
    });
  }

  saveDraft() {
    const th = this.theme();
    const t  = this.tok();
    if (!th || !t) return;

    this.saving.set(true);
    this.error.set(null);
    this.api.updateTheme(th.id, { tokens: t }).subscribe({
      next: (updated) => { this.theme.set(updated); this.saving.set(false); },
      error: (e) => { this.error.set(e.message ?? 'Save failed'); this.saving.set(false); },
    });
  }

  publish() {
    const th = this.theme();
    if (!th) return;

    this.publishing.set(true);
    this.error.set(null);
    this.api.publishTheme(th.id).subscribe({
      next: (updated) => { this.theme.set(updated); this.publishing.set(false); },
      error: (e) => { this.error.set(e.message ?? 'Publish failed'); this.publishing.set(false); },
    });
  }

  copyCss() {
    navigator.clipboard?.writeText(this.cssPreview()).then(() => {
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 1500);
    });
  }

  downloadExport(format: 'css' | 'json') {
    const th = this.theme();
    if (!th) return;

    this.api.exportTheme(th.id, format).subscribe({
      next: (content) => {
        const mime = format === 'css' ? 'text/css' : 'application/json';
        const ext  = format;
        const blob = new Blob([content], { type: mime });
        const url  = URL.createObjectURL(blob);
        const a    = document.createElement('a');
        a.href     = url;
        a.download = `${th.name.toLowerCase().replace(/\s+/g, '-')}.${ext}`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: (e) => this.error.set(e.message ?? 'Export failed'),
    });
  }
}
