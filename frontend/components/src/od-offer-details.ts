import { LitElement, html, css, nothing } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import type { OfferDto } from './od-types.js';
import { BOARD_LABELS, CAT_LABELS } from './od-types.js';

/**
 * Full single-offer detail view.
 * Accepts an offer object directly or fetches by `offer-id` + `api-base`.
 *
 * @element od-offer-details
 *
 * @attr {string} offer-id  - Offer ID; fetches `{api-base}/api/v1/offers/{offer-id}`.
 * @attr {string} [api-base=""] - Origin/prefix for API calls.
 * @attr {string} [cta-label="Запитване"] - Label for the primary action button.
 *
 * @prop {OfferDto | null} offer - Offer data object (preferred over `offer-id`).
 *
 * @csspart details - Root `<article>`.
 * @csspart image   - Hero `<img>`.
 * @csspart body    - Content container.
 * @csspart title   - Offer `<h2>`.
 * @csspart price   - Price `<span>`.
 * @csspart meta    - Meta chips row.
 * @csspart cta     - Primary action `<button>`.
 *
 * @slot booking-inquiry - Slot for embedding an `<od-booking-inquiry>` or custom form.
 * @slot actions         - Additional action buttons.
 *
 * @fires od-cta-click - Fired when the primary CTA is clicked.
 *   `CustomEvent<{ offer: OfferDto }>`
 */
@customElement('od-offer-details')
export class OdOfferDetails extends LitElement {
  static styles = css`
    :host {
      display: block;
      --odc-font:         system-ui, sans-serif;
      --odc-font-head:    system-ui, sans-serif;
      --odc-accent:       #1a5a61;
      --odc-accent-ink:   #ffffff;
      --odc-accent-soft:  #eef6f6;
      --odc-price:        #0e1618;
      --odc-ink:          #15201f;
      --odc-muted:        #5f6b68;
      --odc-bg:           #ffffff;
      --odc-surface:      #ffffff;
      --odc-border:       rgba(20,30,28,0.12);
      --odc-radius:       14px;
      --odc-radius-sm:    9px;
      --odc-shadow:       0 1px 2px rgba(16,24,22,0.06);
      --odc-tag-bg:       #15201f;
      --odc-tag-ink:      #ffffff;
    }
    *, *::before, *::after { box-sizing: border-box; }

    .details {
      display: grid;
      grid-template-columns: 1fr;
      gap: 24px;
      font-family: var(--odc-font);
      color: var(--odc-ink);
      -webkit-font-smoothing: antialiased;
    }
    @media (min-width: 720px) {
      .details { grid-template-columns: 1fr 340px; }
    }

    /* ── main column ── */
    .main { display: flex; flex-direction: column; gap: 20px; }

    .media {
      position: relative;
      aspect-ratio: 16 / 9;
      border-radius: var(--odc-radius);
      overflow: hidden;
      background: var(--odc-accent-soft);
    }
    .media img {
      width: 100%;
      height: 100%;
      object-fit: cover;
      display: block;
    }
    .tag {
      position: absolute;
      top: 14px;
      left: 14px;
      background: var(--odc-tag-bg);
      color: var(--odc-tag-ink);
      font-size: 11px;
      font-weight: 700;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      padding: 5px 12px;
      border-radius: 999px;
    }

    .body { display: flex; flex-direction: column; gap: 14px; }
    .loc {
      display: flex;
      align-items: center;
      gap: 5px;
      font-size: 13px;
      color: var(--odc-muted);
      font-weight: 500;
    }
    .loc svg { width: 14px; height: 14px; flex: none; }
    h2.title {
      font-family: var(--odc-font-head);
      font-size: clamp(22px, 3vw, 30px);
      font-weight: 700;
      line-height: 1.2;
      margin: 0;
      color: var(--odc-ink);
    }
    .meta {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
    .chip {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      font-size: 13px;
      color: var(--odc-muted);
      background: var(--odc-accent-soft);
      border-radius: 999px;
      padding: 5px 12px;
    }
    .chip svg { width: 14px; height: 14px; flex: none; }
    .desc {
      font-size: 15px;
      line-height: 1.6;
      color: var(--odc-ink);
      margin: 0;
    }

    /* ── sidebar ── */
    .sidebar { display: flex; flex-direction: column; gap: 16px; }
    .price-box {
      background: var(--odc-surface);
      border: 1px solid var(--odc-border);
      border-radius: var(--odc-radius);
      padding: 20px;
      box-shadow: var(--odc-shadow);
    }
    .from { font-size: 12px; color: var(--odc-muted); margin-bottom: 2px; }
    .price {
      font-family: var(--odc-font-head);
      font-size: 32px;
      font-weight: 700;
      color: var(--odc-price);
      line-height: 1;
    }
    .price small { font-size: 14px; font-weight: 500; color: var(--odc-muted); }
    .cta {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 8px;
      width: 100%;
      margin-top: 16px;
      height: 48px;
      background: var(--odc-accent);
      color: var(--odc-accent-ink);
      border: 0;
      border-radius: var(--odc-radius-sm);
      font-family: var(--odc-font);
      font-size: 15px;
      font-weight: 600;
      cursor: pointer;
      transition: filter 0.15s ease;
    }
    .cta:hover { filter: brightness(1.06); }
    .cta svg { width: 16px; height: 16px; flex: none; }

    /* ── status ── */
    .status {
      padding: 32px;
      text-align: center;
      color: var(--odc-muted);
      font-family: var(--odc-font);
      font-size: 14px;
    }
    .status.error { color: #b3261e; }
  `;

  @property({ type: Object }) offer: OfferDto | null = null;
  @property({ attribute: 'offer-id' }) offerId = '';
  @property({ attribute: 'api-base' }) apiBase = '';
  @property({ attribute: 'cta-label' }) ctaLabel = 'Запитване';

  @state() private _loading = false;
  @state() private _error = '';

  connectedCallback() {
    super.connectedCallback();
    if (!this.offer && this.offerId) this._fetch();
  }

  updated(changed: Map<PropertyKey, unknown>) {
    if ((changed.has('offerId') || changed.has('apiBase')) && !this.offer && this.offerId) {
      this._fetch();
    }
  }

  private async _fetch() {
    const url = `${this.apiBase}/api/v1/offers/${this.offerId}`;
    this._loading = true;
    this._error = '';
    try {
      const r = await fetch(url);
      if (!r.ok) throw new Error(`HTTP ${r.status}`);
      this.offer = await r.json();
    } catch (e) {
      this._error = (e as Error).message;
    } finally {
      this._loading = false;
    }
  }

  private _onCta() {
    this.dispatchEvent(new CustomEvent('od-cta-click', {
      detail: { offer: this.offer },
      bubbles: true,
      composed: true,
    }));
  }

  private _pin() {
    return html`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z"/><circle cx="12" cy="10" r="3"/></svg>`;
  }
  private _moon() {
    return html`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/></svg>`;
  }
  private _star() {
    return html`<svg viewBox="0 0 24 24" fill="currentColor" stroke="none" aria-hidden="true"><path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z"/></svg>`;
  }
  private _arrowR() {
    return html`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12h14M12 5l7 7-7 7"/></svg>`;
  }

  render() {
    if (this._loading) return html`<div class="status">Зарежда…</div>`;
    if (this._error)   return html`<div class="status error">${this._error}</div>`;
    if (!this.offer)   return nothing;

    const o = this.offer;
    const nights  = o.nights ?? o.durationNights ?? 0;
    const imgSrc  = o.img ?? o.imageUrl ?? '';
    const board   = BOARD_LABELS[o.board ?? o.boardBasis ?? ''] ?? '';
    const cat     = CAT_LABELS[o.cat ?? ''] ?? '';
    const loc     = o.region ?? o.city ?? '';
    const locFull = loc ? `${loc}, ${o.country}` : o.country;

    return html`
      <article class="details" part="details">
        <div class="main">
          <div class="media">
            ${imgSrc ? html`<img part="image" src=${imgSrc} alt=${o.title} />` : nothing}
            ${cat ? html`<span class="tag">${cat}</span>` : nothing}
          </div>

          <div class="body" part="body">
            ${locFull ? html`<span class="loc">${this._pin()}${locFull}</span>` : nothing}
            <h2 class="title" part="title">${o.title}</h2>

            <div class="meta" part="meta">
              ${nights ? html`<span class="chip">${this._moon()}${nights} нощувки</span>` : nothing}
              ${board  ? html`<span class="chip">${board}</span>` : nothing}
              ${o.rating ? html`<span class="chip" style="color:var(--odc-accent)">${this._star()}${o.rating.toFixed(1)}</span>` : nothing}
              ${o.transport ? html`<span class="chip">${o.transport === 'plane' ? 'Самолет' : 'Автобус'}</span>` : nothing}
            </div>

            ${o.description ? html`<p class="desc">${o.description}</p>` : nothing}
          </div>
        </div>

        <aside class="sidebar">
          <div class="price-box">
            <div class="from">от</div>
            <div class="price" part="price">€${o.price}<small> / човек</small></div>
            <button class="cta" part="cta" @click=${this._onCta}>
              ${this.ctaLabel}${this._arrowR()}
            </button>
            <slot name="actions"></slot>
          </div>

          <slot name="booking-inquiry"></slot>
        </aside>
      </article>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'od-offer-details': OdOfferDetails;
  }
}
