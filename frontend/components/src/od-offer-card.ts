import { LitElement, html, css, nothing } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import type { OfferDto } from './od-types.js';
import { BOARD_LABELS, CAT_LABELS } from './od-types.js';

/**
 * Single offer card. Mirrors the `.odc-card` design spec.
 * All colours, typography, spacing and radii read from `--odc-*` CSS custom properties.
 *
 * @element od-offer-card
 *
 * @attr {string} [cta-label="Виж офертата"] - Label for the call-to-action button.
 * @attr {string} offer-id - Offer ID; when set and `offer` prop is absent the element fetches
 *   `{api-base}/api/v1/offers/{offer-id}`.
 * @attr {string} [api-base=""] - Origin/prefix used when fetching by `offer-id`.
 * @attr {string} [card-style="default"] - Visual variant: "default" | "compact" | "editorial".
 *
 * @prop {OfferDto | null} offer - Offer data object (preferred over `offer-id`).
 *
 * @csspart card  - Root `<article>`.
 * @csspart image - `<img>` element.
 * @csspart body  - Card body container.
 * @csspart title - Offer title `<h3>`.
 * @csspart price - Price `<span>`.
 * @csspart cta   - CTA `<button>`.
 * @csspart tag   - Category badge `<span>`.
 * @csspart meta  - Chips wrapper `<div>`.
 *
 * @slot actions - Extra content rendered below the CTA inside the card body.
 *
 * @fires od-cta-click - Dispatched when the CTA button is clicked.
 *   `CustomEvent<{ offer: OfferDto }>`
 */
@customElement('od-offer-card')
export class OdOfferCard extends LitElement {
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
      --odc-shadow-hover: 0 12px 30px rgba(16,24,22,0.14);
      --odc-tag-bg:       #15201f;
      --odc-tag-ink:      #ffffff;
    }
    *, *::before, *::after { box-sizing: border-box; }

    /* ── base card ── */
    .card {
      display: flex;
      flex-direction: column;
      height: 100%;
      background: var(--odc-surface);
      border: 1px solid var(--odc-border);
      border-radius: var(--odc-radius);
      overflow: hidden;
      box-shadow: var(--odc-shadow);
      transition: transform 0.2s ease, box-shadow 0.2s ease;
      font-family: var(--odc-font);
      color: var(--odc-ink);
      -webkit-font-smoothing: antialiased;
    }
    .card:hover {
      transform: translateY(-3px);
      box-shadow: var(--odc-shadow-hover);
    }

    /* ── media ── */
    .media {
      position: relative;
      aspect-ratio: 4 / 3;
      overflow: hidden;
      background: var(--odc-accent-soft);
      flex: none;
    }
    .media img {
      width: 100%;
      height: 100%;
      object-fit: cover;
      transition: transform 0.6s ease;
      display: block;
    }
    .card:hover .media img { transform: scale(1.04); }

    .tag {
      position: absolute;
      top: 12px;
      left: 12px;
      background: var(--odc-tag-bg);
      color: var(--odc-tag-ink);
      font-size: 11px;
      font-weight: 700;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      padding: 5px 10px;
      border-radius: 999px;
      pointer-events: none;
    }
    .fav {
      position: absolute;
      top: 10px;
      right: 10px;
      width: 34px;
      height: 34px;
      display: grid;
      place-items: center;
      background: rgba(255,255,255,0.9);
      border-radius: 999px;
      color: var(--odc-ink);
      border: 0;
      cursor: pointer;
      backdrop-filter: blur(4px);
      padding: 0;
    }
    .fav svg { width: 17px; height: 17px; }

    /* ── body ── */
    .body {
      padding: 16px;
      display: flex;
      flex-direction: column;
      gap: 10px;
      flex: 1;
    }
    .loc {
      display: flex;
      align-items: center;
      gap: 5px;
      font-size: 12.5px;
      color: var(--odc-muted);
      font-weight: 500;
    }
    .loc svg { width: 14px; height: 14px; flex: none; }
    .title {
      font-family: var(--odc-font-head);
      font-size: 18px;
      font-weight: 600;
      line-height: 1.25;
      color: var(--odc-ink);
      margin: 0;
    }
    .meta {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
    }
    .chip {
      display: inline-flex;
      align-items: center;
      gap: 5px;
      font-size: 12px;
      color: var(--odc-muted);
      background: var(--odc-accent-soft);
      border-radius: 999px;
      padding: 4px 10px;
      white-space: nowrap;
    }
    .chip svg { width: 13px; height: 13px; flex: none; }

    /* ── footer ── */
    .foot {
      margin-top: auto;
      padding-top: 12px;
      border-top: 1px solid var(--odc-border);
      display: flex;
      align-items: flex-end;
      justify-content: space-between;
      gap: 10px;
    }
    .price-wrap { display: flex; flex-direction: column; gap: 1px; }
    .from { font-size: 11px; color: var(--odc-muted); }
    .price {
      font-family: var(--odc-font-head);
      font-size: 22px;
      font-weight: 700;
      color: var(--odc-price);
      line-height: 1.1;
    }
    .price small { font-size: 12px; font-weight: 500; color: var(--odc-muted); }
    .cta {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      background: var(--odc-accent);
      color: var(--odc-accent-ink);
      border: 0;
      border-radius: var(--odc-radius-sm);
      padding: 9px 14px;
      font-family: var(--odc-font);
      font-size: 13.5px;
      font-weight: 600;
      cursor: pointer;
      transition: filter 0.15s ease;
      white-space: nowrap;
      flex: none;
    }
    .cta:hover { filter: brightness(1.06); }
    .cta svg { width: 15px; height: 15px; flex: none; }

    /* ── compact variant ── */
    :host([card-style="compact"]) .media { aspect-ratio: 16 / 9; }
    :host([card-style="compact"]) .body { padding: 13px; gap: 7px; }
    :host([card-style="compact"]) .title { font-size: 15.5px; }
    :host([card-style="compact"]) .chip { background: transparent; padding: 0; gap: 4px; }
    :host([card-style="compact"]) .price { font-size: 19px; }

    /* ── editorial variant ── */
    :host([card-style="editorial"]) .card {
      border: none;
      box-shadow: none;
      background: transparent;
    }
    :host([card-style="editorial"]) .media { aspect-ratio: 3 / 4; border-radius: var(--odc-radius); }
    :host([card-style="editorial"]) .card:hover { transform: none; }
    :host([card-style="editorial"]) .card:hover .media img { transform: scale(1.05); }
    :host([card-style="editorial"]) .tag {
      top: auto;
      bottom: 12px;
      left: 12px;
      background: rgba(255,255,255,0.92);
      color: var(--odc-ink);
    }
    :host([card-style="editorial"]) .body { padding: 13px 2px 4px; gap: 6px; }
    :host([card-style="editorial"]) .title { font-size: 17px; }
    :host([card-style="editorial"]) .meta { display: none; }
    :host([card-style="editorial"]) .foot { border-top: none; padding-top: 6px; align-items: center; }
    :host([card-style="editorial"]) .from { display: none; }
    :host([card-style="editorial"]) .price { font-size: 17px; }
    :host([card-style="editorial"]) .cta {
      background: transparent;
      color: var(--odc-accent);
      padding: 6px 0;
    }

    /* ── status ── */
    .status {
      padding: 20px;
      text-align: center;
      color: var(--odc-muted);
      font-family: var(--odc-font);
      font-size: 14px;
    }
    .status.error { color: #b3261e; }
  `;

  /** Offer data object. When set, `offer-id` is ignored. */
  @property({ type: Object }) offer: OfferDto | null = null;

  /** Offer ID — triggers a fetch when `offer` prop is absent. */
  @property({ attribute: 'offer-id' }) offerId = '';

  /** Base URL for API calls (used with `offer-id`). */
  @property({ attribute: 'api-base' }) apiBase = '';

  /** CTA button label. */
  @property({ attribute: 'cta-label' }) ctaLabel = 'Виж офертата';

  /** Visual variant. */
  @property({ attribute: 'card-style', reflect: true })
  cardStyle: 'default' | 'compact' | 'editorial' = 'default';

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

  private _onCta(e: Event) {
    e.stopPropagation();
    this.dispatchEvent(new CustomEvent('od-cta-click', {
      detail: { offer: this.offer },
      bubbles: true,
      composed: true,
    }));
  }

  // ── data helpers ──────────────────────────────────────────────────────────

  private _nights(o: OfferDto) { return o.nights ?? o.durationNights ?? 0; }
  private _img(o: OfferDto)    { return o.img ?? o.imageUrl ?? ''; }
  private _board(o: OfferDto)  { return BOARD_LABELS[o.board ?? o.boardBasis ?? ''] ?? ''; }
  private _cat(o: OfferDto)    { return CAT_LABELS[o.cat ?? ''] ?? ''; }

  private _loc(o: OfferDto) {
    const place = o.region ?? o.city ?? '';
    return place ? `${place}, ${o.country}` : o.country;
  }

  // ── SVG icons (aria-hidden, no external deps) ─────────────────────────────

  private _pin() {
    return html`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z"/><circle cx="12" cy="10" r="3"/></svg>`;
  }
  private _moon() {
    return html`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/></svg>`;
  }
  private _plane() {
    return html`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M21 16v-2l-8-5V3.5a1.5 1.5 0 0 0-3 0V9l-8 5v2l8-2.5V19l-2 1.5V22l3.5-1 3.5 1v-1.5L13 19v-5.5z"/></svg>`;
  }
  private _bus() {
    return html`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="1" y="3" width="15" height="13" rx="2"/><path d="M16 8h4l3 3v5h-7V8z"/><circle cx="5.5" cy="18.5" r="2.5"/><circle cx="18.5" cy="18.5" r="2.5"/></svg>`;
  }
  private _heart() {
    return html`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/></svg>`;
  }
  private _arrowR() {
    return html`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12h14M12 5l7 7-7 7"/></svg>`;
  }

  render() {
    if (this._loading) return html`<div class="status">Зарежда…</div>`;
    if (this._error)   return html`<div class="status error">${this._error}</div>`;
    if (!this.offer)   return nothing;

    const o      = this.offer;
    const nights = this._nights(o);
    const imgSrc = this._img(o);
    const board  = this._board(o);
    const cat    = this._cat(o);
    const loc    = this._loc(o);

    return html`
      <article class="card" part="card">
        <div class="media">
          ${imgSrc
            ? html`<img part="image" src=${imgSrc} alt=${o.title} loading="lazy" />`
            : nothing}
          ${cat ? html`<span class="tag" part="tag">${cat}</span>` : nothing}
          <button class="fav" aria-label="Запази в любими">${this._heart()}</button>
        </div>

        <div class="body" part="body">
          ${loc ? html`<span class="loc">${this._pin()}${loc}</span>` : nothing}

          <h3 class="title" part="title">${o.title}</h3>

          <div class="meta" part="meta">
            ${nights ? html`<span class="chip">${this._moon()}${nights} нощувки</span>` : nothing}
            ${board  ? html`<span class="chip">${board}</span>` : nothing}
            ${o.transport ? html`
              <span class="chip">
                ${o.transport === 'plane' ? this._plane() : this._bus()}
                ${o.transport === 'plane' ? 'Самолет' : 'Автобус'}
              </span>` : nothing}
          </div>

          <div class="foot">
            <div class="price-wrap">
              <span class="from">от</span>
              <span class="price" part="price">€${o.price}<small> / човек</small></span>
            </div>
            <button class="cta" part="cta" @click=${this._onCta}>
              ${this.ctaLabel}${this._arrowR()}
            </button>
          </div>

          <slot name="actions"></slot>
        </div>
      </article>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'od-offer-card': OdOfferCard;
  }
}
