import { LitElement, html, css, nothing } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import type { OfferDto } from './od-types.js';
import './od-offer-card.js';

/**
 * Responsive grid of offer cards.
 * Fetches from a collection/publication/endpoint or accepts a direct `offers` array.
 *
 * @element od-offer-grid
 *
 * @attr {string} collection   - Collection slug; URL: `{api-base}/api/v1/collections/{slug}/offers`.
 * @attr {string} publication  - Publication ID (alias for `collection`).
 * @attr {string} endpoint     - Full fetch URL (overrides `collection` / `publication`).
 * @attr {string} [api-base=""] - Origin/prefix for API calls.
 * @attr {number} columns      - Fixed column count (omit for responsive auto-fill ≥260 px).
 * @attr {string} [card-style="default"] - Visual style forwarded to each card:
 *   "default" | "compact" | "editorial".
 * @attr {string} title        - Optional section heading.
 * @attr {string} [cta-label="Виж офертата"] - Label forwarded to each card's CTA button.
 *
 * @prop {OfferDto[]} offers - Direct data injection; no fetch is attempted when set.
 *
 * @csspart grid    - The grid `<div>`.
 * @csspart head    - The header row (title + count).
 * @csspart loading - Skeleton loading wrapper.
 * @csspart empty   - Empty-state `<div>`.
 * @csspart error   - Error-state `<div>`.
 *
 * @fires od-cta-click - Bubbles from each card; `detail: { offer: OfferDto }`.
 */
@customElement('od-offer-grid')
export class OdOfferGrid extends LitElement {
  static styles = css`
    :host {
      display: block;
      --odc-font:        system-ui, sans-serif;
      --odc-font-head:   system-ui, sans-serif;
      --odc-accent:      #1a5a61;
      --odc-accent-soft: #eef6f6;
      --odc-ink:         #15201f;
      --odc-muted:       #5f6b68;
      --odc-surface:     #ffffff;
      --odc-border:      rgba(20,30,28,0.12);
      --odc-radius:      14px;
      --odc-radius-sm:   9px;
    }
    *, *::before, *::after { box-sizing: border-box; }

    .head {
      display: flex;
      align-items: baseline;
      justify-content: space-between;
      margin-bottom: 14px;
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
    .count {
      font-size: 13px;
      color: var(--odc-muted);
      white-space: nowrap;
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
      gap: 18px;
    }
    .status {
      padding: 32px;
      text-align: center;
      color: var(--odc-muted);
      font-family: var(--odc-font);
      font-size: 14px;
    }
    .status.error { color: #b3261e; }
    .skel {
      background: var(--odc-accent-soft);
      border-radius: var(--odc-radius);
      height: 340px;
      animation: pulse 1.4s ease-in-out infinite;
    }
    @keyframes pulse {
      0%, 100% { opacity: 1; }
      50%       { opacity: 0.4; }
    }
  `;

  @property({ type: String }) collection?: string;
  @property({ type: String }) publication?: string;
  @property({ type: String }) endpoint?: string;
  @property({ attribute: 'api-base' }) apiBase = '';
  @property({ type: Number }) columns?: number;
  @property({ attribute: 'card-style' }) cardStyle: 'default' | 'compact' | 'editorial' = 'default';
  @property({ type: String }) title?: string;
  @property({ attribute: 'cta-label' }) ctaLabel = 'Виж офертата';

  /** Directly injected offers — skips fetch when set. */
  @property({ type: Array }) offers?: OfferDto[];

  @state() private _fetched: OfferDto[] = [];
  @state() private _loading = false;
  @state() private _error = '';

  private get _url(): string {
    if (this.endpoint) return this.endpoint;
    const slug = this.publication ?? this.collection;
    if (slug) return `${this.apiBase}/api/v1/collections/${slug}/offers`;
    return '';
  }

  connectedCallback() {
    super.connectedCallback();
    if (!this.offers && this._url) this._load();
  }

  updated(changed: Map<PropertyKey, unknown>) {
    const watched = ['collection', 'publication', 'endpoint', 'apiBase'];
    if (watched.some(k => changed.has(k)) && !this.offers) {
      this._load();
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

  private _gridStyle() {
    return this.columns
      ? `grid-template-columns: repeat(${this.columns}, minmax(0, 1fr))`
      : '';
  }

  render() {
    const list = this.offers ?? this._fetched;

    if (this._loading && list.length === 0) {
      const skeletons = this.columns ?? 3;
      return html`
        ${this.title ? html`<div class="head" part="head"><h2 class="head-title">${this.title}</h2></div>` : nothing}
        <div class="grid" style=${this._gridStyle()} part="loading">
          ${Array.from({ length: skeletons }, () => html`<div class="skel"></div>`)}
        </div>
      `;
    }

    if (this._error) {
      return html`<div class="status error" part="error">Грешка при зареждане: ${this._error}</div>`;
    }

    if (list.length === 0) {
      return html`<div class="status" part="empty">Няма намерени оферти.</div>`;
    }

    return html`
      ${this.title ? html`
        <div class="head" part="head">
          <h2 class="head-title">${this.title}</h2>
          <span class="count">${list.length} оферти</span>
        </div>` : nothing}
      <div class="grid" style=${this._gridStyle()} part="grid">
        ${list.map(o => html`
          <od-offer-card
            .offer=${o}
            cta-label=${this.ctaLabel}
            card-style=${this.cardStyle}
          ></od-offer-card>
        `)}
      </div>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'od-offer-grid': OdOfferGrid;
  }
}
