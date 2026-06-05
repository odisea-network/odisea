import { LitElement, html, css, nothing } from 'lit';
import { customElement, property, state, query } from 'lit/decorators.js';
import type { OfferDto } from './od-types.js';
import './od-offer-card.js';

/**
 * Horizontal carousel of offer cards with peek, touch-swipe and keyboard navigation.
 * Accepts the same data inputs as `od-offer-grid`.
 *
 * @element od-offer-carousel
 *
 * @attr {string} collection   - Collection slug; URL: `{api-base}/api/v1/collections/{slug}/offers`.
 * @attr {string} publication  - Publication ID (alias for `collection`).
 * @attr {string} endpoint     - Full fetch URL (overrides `collection` / `publication`).
 * @attr {string} [api-base=""] - Origin/prefix for API calls.
 * @attr {string} [card-style="default"] - Visual style forwarded to each card.
 * @attr {string} title        - Optional section heading.
 * @attr {string} [cta-label="Виж офертата"] - Label forwarded to each card's CTA button.
 * @attr {number} [card-width=248] - Fixed card width in px.
 *
 * @prop {OfferDto[]} offers - Direct data injection; no fetch is attempted when set.
 *
 * @csspart track    - The scrollable flex track.
 * @csspart head     - The header row (title + nav buttons).
 * @csspart nav-prev - Previous button.
 * @csspart nav-next - Next button.
 * @csspart loading  - Loading wrapper.
 * @csspart empty    - Empty-state container.
 * @csspart error    - Error-state container.
 *
 * @fires od-cta-click - Bubbles from each card; `detail: { offer: OfferDto }`.
 */
@customElement('od-offer-carousel')
export class OdOfferCarousel extends LitElement {
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
    }
    *, *::before, *::after { box-sizing: border-box; }

    .head {
      display: flex;
      align-items: center;
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
    .nav-row {
      display: flex;
      gap: 8px;
      flex: none;
    }
    .nav-btn {
      width: 38px;
      height: 38px;
      border-radius: 999px;
      border: 1px solid var(--odc-border);
      background: var(--odc-surface);
      color: var(--odc-ink);
      display: grid;
      place-items: center;
      cursor: pointer;
      box-shadow: var(--odc-shadow);
      transition: background 0.15s ease, color 0.15s ease;
      padding: 0;
    }
    .nav-btn:hover {
      background: var(--odc-accent);
      color: var(--odc-accent-ink);
      border-color: var(--odc-accent);
    }
    .nav-btn:disabled { opacity: 0.35; cursor: not-allowed; }
    .nav-btn:disabled:hover { background: var(--odc-surface); color: var(--odc-ink); border-color: var(--odc-border); }
    .nav-btn svg { width: 18px; height: 18px; }

    .wrap { position: relative; overflow: hidden; }
    .track {
      display: flex;
      gap: 16px;
      overflow-x: auto;
      scroll-snap-type: x mandatory;
      scroll-behavior: smooth;
      padding: 4px 2px 14px;
      scrollbar-width: thin;
      scrollbar-color: var(--odc-border) transparent;
      -webkit-overflow-scrolling: touch;
    }
    .track::-webkit-scrollbar { height: 6px; }
    .track::-webkit-scrollbar-thumb { background: var(--odc-border); border-radius: 999px; }
    .track > od-offer-card {
      scroll-snap-align: start;
      flex: 0 0 var(--_card-w, 248px);
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
      flex: 0 0 248px;
      height: 340px;
      background: var(--odc-accent-soft);
      border-radius: var(--odc-radius);
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
  @property({ attribute: 'card-style' }) cardStyle: 'default' | 'compact' | 'editorial' = 'default';
  @property({ type: String }) title?: string;
  @property({ attribute: 'cta-label' }) ctaLabel = 'Виж офертата';
  @property({ attribute: 'card-width', type: Number }) cardWidth = 248;

  /** Directly injected offers — skips fetch when set. */
  @property({ type: Array }) offers?: OfferDto[];

  @state() private _fetched: OfferDto[] = [];
  @state() private _loading = false;
  @state() private _error = '';
  @state() private _canPrev = false;
  @state() private _canNext = true;

  @query('.track') private _track!: HTMLElement;

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
    if (changed.has('cardWidth')) {
      this.style.setProperty('--_card-w', `${this.cardWidth}px`);
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

  private _onScroll() {
    const t = this._track;
    if (!t) return;
    this._canPrev = t.scrollLeft > 8;
    this._canNext = t.scrollLeft + t.clientWidth < t.scrollWidth - 8;
  }

  private _scroll(dir: -1 | 1) {
    const t = this._track;
    if (!t) return;
    t.scrollBy({ left: dir * (this.cardWidth + 16), behavior: 'smooth' });
  }

  // Touch-swipe state
  private _touchStartX = 0;

  private _onTouchStart(e: TouchEvent) {
    this._touchStartX = e.touches[0].clientX;
  }
  private _onTouchEnd(e: TouchEvent) {
    const dx = e.changedTouches[0].clientX - this._touchStartX;
    if (Math.abs(dx) > 40) this._scroll(dx < 0 ? 1 : -1);
  }

  private _onKeyDown(e: KeyboardEvent) {
    if (e.key === 'ArrowLeft')  { e.preventDefault(); this._scroll(-1); }
    if (e.key === 'ArrowRight') { e.preventDefault(); this._scroll(1); }
  }

  private _chevL() {
    return html`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M15 18l-6-6 6-6"/></svg>`;
  }
  private _chevR() {
    return html`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M9 18l6-6-6-6"/></svg>`;
  }

  render() {
    const list = this.offers ?? this._fetched;

    const head = this.title ? html`
      <div class="head" part="head">
        <h2 class="head-title">${this.title}</h2>
        <div class="nav-row">
          <button
            class="nav-btn"
            part="nav-prev"
            aria-label="Предишни"
            ?disabled=${!this._canPrev}
            @click=${() => this._scroll(-1)}
          >${this._chevL()}</button>
          <button
            class="nav-btn"
            part="nav-next"
            aria-label="Следващи"
            ?disabled=${!this._canNext}
            @click=${() => this._scroll(1)}
          >${this._chevR()}</button>
        </div>
      </div>` : nothing;

    if (this._loading && list.length === 0) {
      return html`
        ${head}
        <div class="wrap">
          <div class="track" part="loading">
            ${Array.from({ length: 4 }, () => html`<div class="skel"></div>`)}
          </div>
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
      ${head}
      <div
        class="wrap"
        @touchstart=${this._onTouchStart}
        @touchend=${this._onTouchEnd}
        @keydown=${this._onKeyDown}
        tabindex="0"
        role="region"
        aria-label=${this.title ?? 'Оферти'}
      >
        <div class="track" part="track" @scroll=${this._onScroll}>
          ${list.map(o => html`
            <od-offer-card
              .offer=${o}
              cta-label=${this.ctaLabel}
              card-style=${this.cardStyle}
            ></od-offer-card>
          `)}
        </div>
      </div>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'od-offer-carousel': OdOfferCarousel;
  }
}
