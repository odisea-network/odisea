import { Component, Input, Output, EventEmitter, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';

interface EmbedMethod {
  id: string;
  label: string;
  tag?: string;
  soon?: boolean;
}

const METHODS: EmbedMethod[] = [
  { id: 'wc',      label: 'Web компонент',  tag: 'Препоръчано' },
  { id: 'wp',      label: 'WordPress',       tag: 'Приоритет' },
  { id: 'iframe',  label: 'iFrame fallback' },
  { id: 'npm',     label: 'JS пакет' },
  { id: 'api',     label: 'REST API' },
  { id: 'webhook', label: 'Webhooks' },
  { id: 'shopify', label: 'Shopify',         soon: true },
  { id: 'seo',     label: 'SEO публикуване', soon: true },
];

const THEME_CSS_SNIPPET = `odisea-collection {
  --odc-accent: #C85727;
  --odc-font: 'Manrope', sans-serif;
  --odc-radius: 20px;
}`;

function buildSnippet(method: string, key: string): { file: string; code: string } {
  switch (method) {
    case 'wc': return {
      file: 'index.html',
      code:
`<!-- 1. Зареди веднъж в <head> -->
<script src="https://cdn.odisea.travel/v1/odisea.js" async></script>

<!-- 2. Постави колекцията където пожелаеш -->
<odisea-collection
  key="${key}"
  theme="brand"
  columns="3">
</odisea-collection>`,
    };
    case 'wp': return {
      file: 'WordPress · shortcode',
      code:
`// Постави в страница, публикация или текстов блок
[odisea collection="${key}" theme="brand" columns="3"]

// Или чрез Gutenberg блока „Odisea Колекция"
// след инсталиране на плъгина Odisea for WordPress`,
    };
    case 'iframe': return {
      file: 'index.html',
      code:
`<!-- за среди без скриптове / със CSS конфликти -->
<iframe
  src="https://embed.odisea.travel/c/${key}"
  width="100%" height="760" loading="lazy"
  style="border:0"></iframe>`,
    };
    case 'npm': return {
      file: 'app.tsx',
      code:
`import { OdiseaCollection } from '@odisea/react'

<OdiseaCollection
  collectionKey="${key}"
  theme="brand" columns={3}
  onInquiry={handleLead} />`,
    };
    case 'api': return {
      file: 'cURL',
      code:
`curl https://api.odisea.travel/v1/publications/${key} \\
  -H "Authorization: Bearer sk_live_••••••••"

# → 200 OK
# { "offersUrl": "…", "version": 1 }`,
    };
    case 'webhook': return {
      file: 'webhook · lead.created',
      code:
`// Конфигурирай endpoint за събития
POST https://your-crm.bg/hooks/odisea

{
  "event": "lead.created",
  "collection": "${key}",
  "lead": { "name", "email", "offerId" }
}`,
    };
    case 'shopify': return {
      file: 'Shopify · theme app block (предстои)',
      code:
`// Предстои — валидираме реално търсене
// Добави блок „Odisea Колекция" в темата
collection_key: "${key}"`,
    };
    case 'seo': return {
      file: 'SEO публикуване (предстои)',
      code:
`// Native страници, не iframe — за реално SEO
// canonical URL · structured data · sitemap
mode: "server-rendered"
sync: "WordPress"`,
    };
    default: return { file: '', code: '' };
  }
}

@Component({
  selector: 'app-embed-modal',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="overlay" (click)="close.emit()">
      <div class="dialog" (click)="$event.stopPropagation()">

        <!-- Header -->
        <div class="dialog-header">
          <div>
            <div class="eyebrow">Вграждане</div>
            <h2>{{ publicationName || 'Публикация' }}</h2>
            <div class="header-meta">
              <span class="badge-success">✓ Активна</span>
              <span class="meta-text">обновява се автоматично</span>
            </div>
          </div>
          <button class="btn-icon" (click)="close.emit()">✕</button>
        </div>

        <!-- Body: sidebar + content -->
        <div class="dialog-body">
          <!-- Method sidebar -->
          <div class="method-sidebar">
            @for (m of methods; track m.id) {
              <button class="method-btn" [class.active]="activeMethod() === m.id"
                (click)="activeMethod.set(m.id)">
                <span class="method-label">{{ m.label }}</span>
                @if (m.tag) {
                  <span class="method-tag">{{ m.tag }}</span>
                }
                @if (m.soon) {
                  <span class="method-soon">Скоро</span>
                }
              </button>
            }
          </div>

          <!-- Code panel -->
          <div class="code-panel">
            <div class="code-block">
              <div class="code-header">
                <span class="code-filename">{{ currentSnippet().file }}</span>
                <button class="btn-copy" [class.copied]="copied()" (click)="doCopy()">
                  {{ copied() ? '✓ Копирано' : 'Копирай' }}
                </button>
              </div>
              <pre class="code-body">{{ currentSnippet().code }}</pre>
            </div>

            <!-- Theme CSS hint -->
            <details class="theme-hint">
              <summary>Стилизирай към своя бранд</summary>
              <p class="hint-text">Компонентите четат CSS променливи — задай ги веднъж и целият набор приема вашия облик.</p>
              <div class="code-block">
                <div class="code-header">
                  <span class="code-filename">theme.css</span>
                </div>
                <pre class="code-body">{{ themeCssSnippet }}</pre>
              </div>
            </details>
          </div>
        </div>

        <!-- Footer -->
        <div class="dialog-footer">
          <a class="btn-ghost" href="https://docs.odisea.travel" target="_blank" rel="noopener">
            ↗ Документация за разработчици
          </a>
          <div class="footer-actions">
            <button class="btn-secondary" (click)="close.emit()">Затвори</button>
            <button class="btn-primary" [class.copied]="copied()" (click)="doCopy()">
              {{ copied() ? '✓ Копирано' : 'Копирай кода' }}
            </button>
          </div>
        </div>

      </div>
    </div>
  `,
  styles: [`
    .overlay {
      position: fixed; inset: 0; z-index: 200;
      background: rgba(14,22,24,.5); backdrop-filter: blur(3px);
      display: flex; align-items: center; justify-content: center;
      padding: 24px; animation: odFade 160ms ease;
    }
    .dialog {
      background: var(--od-surface, #fff);
      box-shadow: var(--od-shadow-pop, 0 16px 48px rgba(14,22,24,.18));
      width: min(920px, 100%); height: min(640px, 92vh);
      border-radius: var(--od-r-xl, 16px); overflow: hidden;
      display: flex; flex-direction: column;
      animation: odPop 200ms cubic-bezier(.2,.8,.2,1);
    }
    @keyframes odFade { from { opacity: 0; } }
    @keyframes odPop  { from { opacity: 0; transform: scale(.96); } }

    .dialog-header {
      display: flex; align-items: flex-start; justify-content: space-between;
      padding: 20px 24px 16px; border-bottom: 1px solid var(--od-border, #e1e6e4); flex: none;
    }
    .eyebrow {
      font-size: 11px; font-weight: 700; letter-spacing: .08em;
      text-transform: uppercase; color: var(--od-500, #6c7976); margin-bottom: 5px;
    }
    h2 { margin: 0; font-size: 19px; font-weight: 700; color: var(--od-ink, #0e1618); }
    .header-meta { display: flex; align-items: center; gap: 8px; margin-top: 7px; }
    .badge-success {
      display: inline-flex; padding: 3px 8px; border-radius: 99px;
      font-size: 12px; font-weight: 600;
      background: var(--od-success-bg, #e7f3ec); color: var(--od-success, #1c7a4a);
    }
    .meta-text { font-size: 12.5px; color: var(--od-500, #6c7976); }
    .btn-icon {
      width: 32px; height: 32px; border: 0; border-radius: var(--od-r-md, 8px);
      background: transparent; color: var(--od-500, #6c7976); cursor: pointer;
      font-size: 16px; display: grid; place-items: center;
      transition: background var(--od-fast, 120ms ease);
    }
    .btn-icon:hover { background: var(--od-100, #f1f4f3); }

    .dialog-body { flex: 1; min-height: 0; display: flex; overflow: hidden; }

    .method-sidebar {
      width: 188px; flex: none; border-right: 1px solid var(--od-border, #e1e6e4);
      padding: 14px 12px; display: flex; flex-direction: column; gap: 3px;
      background: var(--od-50, #f7f9f8); overflow-y: auto;
    }
    .method-btn {
      display: flex; align-items: center; gap: 7px; padding: 9px 10px;
      border: 0; border-radius: var(--od-r-md, 8px); cursor: pointer; text-align: left;
      background: transparent; color: var(--od-700, #3a4744);
      font-family: var(--od-font, system-ui); font-size: 13px; font-weight: 500;
      transition: all var(--od-fast, 120ms ease);
    }
    .method-btn.active {
      background: var(--od-surface, #fff); color: var(--od-teal-700, #14474e); font-weight: 600;
      box-shadow: var(--od-shadow-xs, 0 1px 2px rgba(14,22,24,.05));
    }
    .method-label { flex: 1; min-width: 0; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .method-tag {
      font-size: 9px; font-weight: 700; letter-spacing: .03em; text-transform: uppercase;
      color: var(--od-gold-700, #9a7434); background: var(--od-gold-50, #faf4e7);
      padding: 2px 5px; border-radius: 4px; flex: none;
    }
    .method-soon {
      font-size: 9px; font-weight: 700; text-transform: uppercase;
      color: var(--od-500, #6c7976); background: var(--od-100, #f1f4f3);
      padding: 2px 5px; border-radius: 4px; flex: none;
    }

    .code-panel {
      flex: 1; min-width: 0; padding: 20px; overflow-y: auto;
      display: flex; flex-direction: column; gap: 14px;
    }
    .code-block {
      border-radius: var(--od-r-md, 8px); overflow: hidden;
      border: 1px solid var(--od-800, #283330); background: var(--od-900, #16211f);
    }
    .code-header {
      display: flex; align-items: center; justify-content: space-between;
      padding: 8px 12px; background: var(--od-ink, #0e1618);
      border-bottom: 1px solid rgba(255,255,255,.08);
    }
    .code-filename { font-family: var(--od-mono, monospace); font-size: 12px; color: rgba(255,255,255,.6); }
    .btn-copy {
      display: inline-flex; align-items: center; gap: 6px; height: 28px; padding: 0 10px;
      border: 1px solid rgba(255,255,255,.15); border-radius: 6px;
      background: rgba(255,255,255,.06); color: #fff;
      font-family: var(--od-font, system-ui); font-size: 12px; font-weight: 600; cursor: pointer;
      transition: all var(--od-fast, 120ms ease);
    }
    .btn-copy.copied { background: var(--od-teal-600, #1a5a61); border-color: var(--od-teal-600, #1a5a61); }
    .code-body {
      margin: 0; padding: 14px 16px; overflow: auto;
      font-family: var(--od-mono, monospace); font-size: 12.5px; line-height: 1.7;
      color: #cfe8e6; white-space: pre;
    }

    .theme-hint {
      border: 1px solid var(--od-border, #e1e6e4); border-radius: var(--od-r-md, 8px); overflow: hidden;
    }
    .theme-hint > summary {
      display: flex; align-items: center; gap: 9px; padding: 12px 14px;
      background: var(--od-surface, #fff); cursor: pointer;
      font-family: var(--od-font, system-ui); font-size: 13.5px; font-weight: 600;
      color: var(--od-ink, #0e1618); list-style: none;
    }
    .theme-hint > summary::-webkit-details-marker { display: none; }
    .hint-text { font-size: 12.5px; color: var(--od-500, #6c7976); margin: 8px 14px 10px; }
    .theme-hint .code-block { margin: 0 14px 14px; }

    .dialog-footer {
      display: flex; align-items: center; justify-content: space-between; gap: 12px;
      padding: 14px 22px; border-top: 1px solid var(--od-border, #e1e6e4);
      background: var(--od-50, #f7f9f8); flex: none;
    }
    .footer-actions { display: flex; gap: 9px; }
    .btn-ghost {
      border: 0; background: transparent; color: var(--od-600, #525f5c);
      font-family: var(--od-font, system-ui); font-size: 13px; font-weight: 600;
      cursor: pointer; padding: 7px 10px; border-radius: var(--od-r-md, 8px);
      text-decoration: none; transition: background var(--od-fast, 120ms ease);
    }
    .btn-ghost:hover { background: var(--od-100, #f1f4f3); }
    .btn-secondary {
      padding: 8px 14px; border-radius: var(--od-r-md, 8px);
      border: 1px solid var(--od-200, #e1e6e4); background: #fff;
      color: var(--od-ink, #0e1618); font-family: var(--od-font, system-ui);
      font-size: 13px; font-weight: 600; cursor: pointer;
      transition: background var(--od-fast, 120ms ease);
    }
    .btn-secondary:hover { background: var(--od-50, #f7f9f8); }
    .btn-primary {
      padding: 8px 16px; border-radius: var(--od-r-md, 8px); border: none;
      background: var(--od-teal-600, #1a5a61); color: #fff;
      font-family: var(--od-font, system-ui); font-size: 13px; font-weight: 600; cursor: pointer;
      transition: background var(--od-fast, 120ms ease);
    }
    .btn-primary:hover { background: var(--od-teal-700, #14474e); }
    .btn-primary.copied { background: var(--od-success, #1c7a4a); }
  `],
})
export class EmbedModalComponent {
  @Input() publicationName = '';
  @Input()
  set publicationKey(v: string) { this._key.set(v); }

  @Output() close = new EventEmitter<void>();

  readonly methods = METHODS;
  readonly themeCssSnippet = THEME_CSS_SNIPPET;

  activeMethod = signal('wc');
  copied = signal(false);

  private _key = signal('demo_key');

  currentSnippet = computed(() => buildSnippet(this.activeMethod(), this._key()));

  doCopy() {
    const code = this.currentSnippet().code;
    navigator.clipboard?.writeText(code).then(() => {
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 1800);
    });
  }
}
