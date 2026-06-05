import { LitElement, html, css, nothing } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import type { CollectionDto } from './od-types.js';

/**
 * A grid of collection cards (linking to publications).
 * Accepts a `collections` array directly or fetches from `endpoint` / `api-base`.
 *
 * @element od-featured-collections
 *
 * @attr {string} endpoint     - Full URL to fetch collections from.
 * @attr {string} [api-base=""] - Origin/prefix; uses `{api-base}/api/v1/collections`.
 * @attr {string} [title]      - Optional section heading.
 * @attr {number} [columns=3]  - Grid column count.
 *
 * @prop {CollectionDto[]} collections - Direct data injection; no fetch when set.
 *
 * @csspart grid  - The grid `<div>`.
 * @csspart head  - The header row.
 * @csspart card  - Each collection card `<a>`.
 * @csspart image - Collection card `<img>`.
 * @csspart label - Collection title `<span>`.
 * @csspart count - Offer count `<span>`.
 *
 * @fires od-collection-click - Fired when a collection card is clicked.
 *   `CustomEvent<{ collection: CollectionDto }>`
 */
@customElement('od-featured-collections')
export class OdFeaturedCollections extends LitElement {
  static styles = css`
    :host {
      display: block;
      --odc-font:        system-ui, sans-serif;
      --odc-font-head:   system-ui, sans-serif;
      --odc-accent:      #1a5a61;
      --odc-accent-ink:  #ffffff;
      --odc-accent-soft: #eef6f6;
      --odc-ink:         #15201f;
      --odc-muted:       #5f6b68;
      --odc-surface:     #ffffff;
      --odc-border:      rgba(20,30,28,0.12);
      --odc-radius:      14px;
      --odc-shadow:      0 1px 2px rgba(16,24,22,0.06);
      --odc-shadow-hover:0 12px 30px rgba(16,24,22,0.14);
    }
    *, *::before, *::after { box-sizing: border-box; }

    .head {
      display: flex;
      align-items: baseline;
      justify-content: space-between;
      margin-bottom: 16px;
      gap: 12px;
      font-family: var(--odc-font);
    }
    .head-title {
      font-family: var(--odc-font-head);
      font-size: 22px;
      font-weight: 700;
      margin: 0;
      color: var(--odc-ink);
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
      gap: 16px;
    }

    /* collection card */
    .card {
      display: flex;
      flex-direction: column;
      border-radius: var(--odc-radius);
      overflow: hidden;
      background: var(--odc-surface);
      border: 1px solid var(--odc-border);
      box-shadow: var(--odc-shadow);
      cursor: pointer;
      text-decoration: none;
      color: inherit;
      transition: transform 0.2s ease, box-shadow 0.2s ease;
    }
    .card:hover {
      transform: translateY(-3px);
      box-shadow: var(--odc-shadow-hover);
    }
    .card-media {
      position: relative;
      aspect-ratio: 3 / 2;
      overflow: hidden;
      background: var(--odc-accent-soft);
      flex: none;
    }
    .card-media img {
      width: 100%;
      height: 100%;
      object-fit: cover;
      transition: transform 0.5s ease;
      display: block;
    }
    .card:hover .card-media img { transform: scale(1.05); }
    .card-placeholder {
      width: 100%;
      height: 100%;
      background: linear-gradient(135deg, var(--odc-accent-soft), var(--odc-border));
      display: grid;
      place-items: center;
    }
    .card-placeholder svg { width: 32px; height: 32px; color: var(--odc-accent); opacity: 0.5; }

    .card-body {
      padding: 12px 14px 14px;
      display: flex;
      flex-direction: column;
      gap: 4px;
      flex: 1;
    }
    .label {
      font-family: var(--odc-font-head);
      font-size: 15px;
      font-weight: 700;
      color: var(--odc-ink);
      line-height: 1.3;
    }
    .desc {
      font-size: 12.5px;
      color: var(--odc-muted);
      line-height: 1.4;
      margin: 0;
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }
    .card-foot {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-top: auto;
      padding-top: 10px;
    }
    .count {
      font-size: 12px;
      color: var(--odc-muted);
    }
    .arrow {
      width: 24px;
      height: 24px;
      display: grid;
      place-items: center;
      border-radius: 999px;
      background: var(--odc-accent-soft);
      color: var(--odc-accent);
      transition: background 0.15s ease;
      flex: none;
    }
    .card:hover .arrow { background: var(--odc-accent); color: var(--odc-accent-ink); }
    .arrow svg { width: 13px; height: 13px; }

    /* status */
    .status {
      padding: 32px;
      text-align: center;
      color: var(--odc-muted);
      font-family: var(--odc-font);
      font-size: 14px;
    }
    .status.error { color: #b3261e; }
    .skel {
      height: 200px;
      background: var(--odc-accent-soft);
      border-radius: var(--odc-radius);
      animation: pulse 1.4s ease-in-out infinite;
    }
    @keyframes pulse {
      0%, 100% { opacity: 1; }
      50%       { opacity: 0.4; }
    }
  `;

  @property({ type: String }) endpoint?: string;
  @property({ attribute: 'api-base' }) apiBase = '';
  @property({ type: String }) title?: string;
  @property({ type: Number }) columns = 3;

  /** Direct data injection — skips fetch when set. */
  @property({ type: Array }) collections?: CollectionDto[];

  @state() private _fetched: CollectionDto[] = [];
  @state() private _loading = false;
  @state() private _error = '';

  private get _url(): string {
    if (this.endpoint) return this.endpoint;
    if (this.apiBase)  return `${this.apiBase}/api/v1/collections`;
    return '';
  }

  connectedCallback() {
    super.connectedCallback();
    if (!this.collections && this._url) this._load();
  }

  updated(changed: Map<PropertyKey, unknown>) {
    if (['endpoint', 'apiBase'].some(k => changed.has(k)) && !this.collections) {
      this._load();
    }
    if (changed.has('columns')) {
      this.style.setProperty('--_cols', `repeat(${this.columns}, minmax(0, 1fr))`);
    }
  }

  private async _load() {
    const url = this._url;
    if (!url) return;
    this._loading = true;
    this._error = '';
    try {
      const r = await fetch(url);
      if (!r.ok) throw new Error(`HTTP ${r.status}`);
      this._fetched = await r.json();
    } catch (e) {
      this._error = (e as Error).message;
    } finally {
      this._loading = false;
    }
  }

  private _onClick(e: Event, col: CollectionDto) {
    e.preventDefault();
    this.dispatchEvent(new CustomEvent('od-collection-click', {
      detail: { collection: col },
      bubbles: true,
      composed: true,
    }));
  }

  private _gridStyle() {
    return this.columns
      ? `grid-template-columns: repeat(${this.columns}, minmax(0, 1fr))`
      : '';
  }

  private _gridIcon() {
    return html`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" aria-hidden="true"><rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="14" y="14" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/></svg>`;
  }
  private _arrowR() {
    return html`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12h14M12 5l7 7-7 7"/></svg>`;
  }

  render() {
    const list = this.collections ?? this._fetched;

    if (this._loading && list.length === 0) {
      return html`
        ${this.title ? html`<div class="head" part="head"><h2 class="head-title">${this.title}</h2></div>` : nothing}
        <div class="grid" style=${this._gridStyle()} part="grid">
          ${Array.from({ length: this.columns }, () => html`<div class="skel"></div>`)}
        </div>
      `;
    }

    if (this._error) {
      return html`<div class="status error">${this._error}</div>`;
    }

    if (list.length === 0) {
      return html`<div class="status">Няма колекции.</div>`;
    }

    return html`
      ${this.title ? html`
        <div class="head" part="head">
          <h2 class="head-title">${this.title}</h2>
        </div>` : nothing}
      <div class="grid" style=${this._gridStyle()} part="grid">
        ${list.map(col => html`
          <a
            class="card"
            part="card"
            href="#"
            role="button"
            aria-label=${col.title}
            @click=${(e: Event) => this._onClick(e, col)}
          >
            <div class="card-media">
              ${col.imageUrl
                ? html`<img part="image" src=${col.imageUrl} alt=${col.title} loading="lazy" />`
                : html`<div class="card-placeholder">${this._gridIcon()}</div>`}
            </div>
            <div class="card-body">
              <span class="label" part="label">${col.title}</span>
              ${col.description ? html`<p class="desc">${col.description}</p>` : nothing}
              <div class="card-foot">
                ${col.count != null
                  ? html`<span class="count" part="count">${col.count} оферти</span>`
                  : html`<span></span>`}
                <span class="arrow">${this._arrowR()}</span>
              </div>
            </div>
          </a>
        `)}
      </div>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'od-featured-collections': OdFeaturedCollections;
  }
}
