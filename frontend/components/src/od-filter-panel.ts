import { LitElement, html, css, nothing } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import type { OfferDto } from './od-types.js';
import { BOARD_LABELS, CAT_LABELS } from './od-types.js';

/** Active filter state emitted by the `od-filter-change` event. */
export interface FilterState {
  countries: string[];
  boards: string[];
  cats: string[];
  maxPrice: number;
}

/**
 * Sidebar filter panel for an offer list.
 * Derives available facets from the provided `offers` array and emits `od-filter-change`
 * whenever the user changes a filter.
 *
 * @element od-filter-panel
 *
 * @attr {string} title - Panel heading (default: "Филтри").
 * @attr {number} [max-price=3000] - Upper bound of the price range slider.
 * @attr {number} [default-max-price=3000] - Initial slider value.
 *
 * @prop {OfferDto[]} offers - Full offer list used to build facet counts.
 *
 * @csspart panel   - The root `<aside>` element.
 * @csspart heading - The panel `<h4>` heading.
 * @csspart group   - Each filter group `<div>`.
 *
 * @fires od-filter-change - Dispatched on every filter change.
 *   `CustomEvent<FilterState>`
 */
@customElement('od-filter-panel')
export class OdFilterPanel extends LitElement {
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

    .panel {
      background: var(--odc-surface);
      border: 1px solid var(--odc-border);
      border-radius: var(--odc-radius);
      padding: 16px;
      font-family: var(--odc-font);
      color: var(--odc-ink);
      -webkit-font-smoothing: antialiased;
      align-self: start;
    }
    h4 {
      font-family: var(--odc-font-head);
      font-size: 15px;
      font-weight: 700;
      margin: 0 0 12px;
      color: var(--odc-ink);
    }
    .group {
      padding: 12px 0;
      border-top: 1px solid var(--odc-border);
    }
    .group:first-of-type { border-top: none; padding-top: 0; }
    .group-label {
      font-size: 12px;
      font-weight: 700;
      letter-spacing: 0.03em;
      text-transform: uppercase;
      color: var(--odc-muted);
      margin-bottom: 9px;
    }
    .check {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 4px 0;
      font-size: 13.5px;
      color: var(--odc-ink);
      cursor: pointer;
      user-select: none;
    }
    .check input {
      width: 15px;
      height: 15px;
      accent-color: var(--odc-accent);
      cursor: pointer;
      flex: none;
    }
    .count {
      margin-left: auto;
      font-size: 12px;
      color: var(--odc-muted);
    }

    /* price range */
    .range-wrap { display: flex; flex-direction: column; gap: 8px; }
    .range-track {
      -webkit-appearance: none;
      appearance: none;
      width: 100%;
      height: 4px;
      border-radius: 999px;
      background: var(--odc-border);
      accent-color: var(--odc-accent);
      cursor: pointer;
    }
    .range-track::-webkit-slider-thumb {
      -webkit-appearance: none;
      width: 18px;
      height: 18px;
      border-radius: 999px;
      background: var(--odc-accent);
      cursor: pointer;
      box-shadow: 0 1px 4px rgba(0,0,0,0.15);
    }
    .range-labels {
      display: flex;
      justify-content: space-between;
      font-size: 12px;
      color: var(--odc-muted);
    }
    .range-value {
      font-size: 13px;
      font-weight: 600;
      color: var(--odc-ink);
      text-align: right;
    }

    .clear-btn {
      margin-top: 14px;
      width: 100%;
      height: 36px;
      border: 1px solid var(--odc-border);
      border-radius: var(--odc-radius-sm);
      background: transparent;
      color: var(--odc-muted);
      font-family: var(--odc-font);
      font-size: 13px;
      cursor: pointer;
      transition: background 0.15s ease, color 0.15s ease;
    }
    .clear-btn:hover { background: var(--odc-accent-soft); color: var(--odc-ink); }
  `;

  @property({ type: String }) title = 'Филтри';
  @property({ attribute: 'max-price', type: Number }) maxPrice = 3000;
  @property({ attribute: 'default-max-price', type: Number }) defaultMaxPrice = 3000;

  /** Full offer list used to derive facet counts. */
  @property({ type: Array }) offers: OfferDto[] = [];

  @state() private _countries: Set<string> = new Set();
  @state() private _boards: Set<string> = new Set();
  @state() private _cats: Set<string> = new Set();
  @state() private _priceMax = 3000;

  connectedCallback() {
    super.connectedCallback();
    this._priceMax = this.defaultMaxPrice || this.maxPrice;
  }

  updated(changed: Map<PropertyKey, unknown>) {
    if (changed.has('defaultMaxPrice') || changed.has('maxPrice')) {
      this._priceMax = this.defaultMaxPrice || this.maxPrice;
    }
  }

  private _emit() {
    this.dispatchEvent(new CustomEvent<FilterState>('od-filter-change', {
      detail: {
        countries:  [...this._countries],
        boards:     [...this._boards],
        cats:       [...this._cats],
        maxPrice:   this._priceMax,
      },
      bubbles: true,
      composed: true,
    }));
  }

  private _toggleCountry(code: string) {
    const next = new Set(this._countries);
    next.has(code) ? next.delete(code) : next.add(code);
    this._countries = next;
    this._emit();
  }
  private _toggleBoard(id: string) {
    const next = new Set(this._boards);
    next.has(id) ? next.delete(id) : next.add(id);
    this._boards = next;
    this._emit();
  }
  private _toggleCat(id: string) {
    const next = new Set(this._cats);
    next.has(id) ? next.delete(id) : next.add(id);
    this._cats = next;
    this._emit();
  }
  private _onPrice(e: Event) {
    this._priceMax = Number((e.target as HTMLInputElement).value);
    this._emit();
  }
  private _clear() {
    this._countries = new Set();
    this._boards    = new Set();
    this._cats      = new Set();
    this._priceMax  = this.defaultMaxPrice || this.maxPrice;
    this._emit();
  }

  // ── facet helpers ─────────────────────────────────────────────────────────

  private _facetCountries() {
    const counts = new Map<string, number>();
    for (const o of this.offers) {
      counts.set(o.country, (counts.get(o.country) ?? 0) + 1);
    }
    return [...counts.entries()].sort((a, b) => b[1] - a[1]);
  }

  private _facetBoards() {
    const counts = new Map<string, number>();
    for (const o of this.offers) {
      const k = o.board ?? o.boardBasis ?? '';
      if (k) counts.set(k, (counts.get(k) ?? 0) + 1);
    }
    return [...counts.entries()].sort((a, b) => b[1] - a[1]);
  }

  private _facetCats() {
    const counts = new Map<string, number>();
    for (const o of this.offers) {
      const k = o.cat ?? '';
      if (k) counts.set(k, (counts.get(k) ?? 0) + 1);
    }
    return [...counts.entries()].sort((a, b) => b[1] - a[1]);
  }

  render() {
    const countries = this._facetCountries();
    const boards    = this._facetBoards();
    const cats      = this._facetCats();
    const hasFilters = this._countries.size > 0 || this._boards.size > 0 ||
                       this._cats.size > 0 || this._priceMax < this.maxPrice;

    return html`
      <aside class="panel" part="panel">
        <h4 part="heading">${this.title}</h4>

        ${countries.length ? html`
          <div class="group" part="group">
            <div class="group-label">Дестинация</div>
            ${countries.map(([code, n]) => html`
              <label class="check">
                <input
                  type="checkbox"
                  .checked=${this._countries.has(code)}
                  @change=${() => this._toggleCountry(code)}
                />
                ${code}
                <span class="count">${n}</span>
              </label>
            `)}
          </div>` : nothing}

        ${cats.length ? html`
          <div class="group" part="group">
            <div class="group-label">Тип почивка</div>
            ${cats.map(([id, n]) => html`
              <label class="check">
                <input
                  type="checkbox"
                  .checked=${this._cats.has(id)}
                  @change=${() => this._toggleCat(id)}
                />
                ${CAT_LABELS[id] ?? id}
                <span class="count">${n}</span>
              </label>
            `)}
          </div>` : nothing}

        ${boards.length ? html`
          <div class="group" part="group">
            <div class="group-label">Изхранване</div>
            ${boards.map(([id, n]) => html`
              <label class="check">
                <input
                  type="checkbox"
                  .checked=${this._boards.has(id)}
                  @change=${() => this._toggleBoard(id)}
                />
                ${BOARD_LABELS[id] ?? id}
                <span class="count">${n}</span>
              </label>
            `)}
          </div>` : nothing}

        <div class="group" part="group">
          <div class="group-label">Бюджет</div>
          <div class="range-wrap">
            <span class="range-value">до €${this._priceMax}</span>
            <input
              type="range"
              class="range-track"
              min="100"
              .max=${String(this.maxPrice)}
              .value=${String(this._priceMax)}
              @input=${this._onPrice}
            />
            <div class="range-labels">
              <span>€100</span>
              <span>€${this.maxPrice}</span>
            </div>
          </div>
        </div>

        ${hasFilters ? html`
          <button class="clear-btn" @click=${this._clear}>Изчисти филтрите</button>
        ` : nothing}
      </aside>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'od-filter-panel': OdFilterPanel;
  }
}
