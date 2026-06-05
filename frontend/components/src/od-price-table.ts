import { LitElement, html, css, nothing } from 'lit';
import { customElement, property } from 'lit/decorators.js';

/** A single departure / price row. */
export interface PriceRow {
  period: string;       // e.g. "02 юни – 09 юни"
  board: string;        // e.g. "Полупансион"
  price: number;        // per-person price in currency
  currency?: string;    // default "EUR"
  best?: boolean;       // highlights this row as best-value
  occupancy?: string;   // e.g. "2+1"
}

/**
 * Tabular price matrix for an offer — departure dates × board basis × occupancy.
 * Accepts a `rows` array directly or derives demo rows from a base-price `offer-price`.
 *
 * @element od-price-table
 *
 * @attr {number} [offer-price] - Base price used to generate demo rows (ignored when `rows` set).
 * @attr {string} [currency="€"] - Currency symbol.
 * @attr {string} [caption]      - Optional visible table caption.
 *
 * @prop {PriceRow[]} rows - Price rows to render.
 *
 * @csspart table  - The `<table>` element.
 * @csspart head   - The `<thead>` row.
 * @csspart row    - Each `<tr>` body row.
 * @csspart best   - The highlighted best-value row.
 * @csspart price  - The price `<td>`.
 */
@customElement('od-price-table')
export class OdPriceTable extends LitElement {
  static styles = css`
    :host {
      display: block;
      --odc-font:        system-ui, sans-serif;
      --odc-font-head:   system-ui, sans-serif;
      --odc-accent:      #1a5a61;
      --odc-accent-soft: #eef6f6;
      --odc-price:       #0e1618;
      --odc-ink:         #15201f;
      --odc-muted:       #5f6b68;
      --odc-surface:     #ffffff;
      --odc-border:      rgba(20,30,28,0.12);
      --odc-radius:      14px;
    }
    *, *::before, *::after { box-sizing: border-box; }

    .wrap {
      overflow-x: auto;
      border-radius: var(--odc-radius);
      border: 1px solid var(--odc-border);
    }
    table {
      width: 100%;
      border-collapse: collapse;
      font-size: 13.5px;
      font-family: var(--odc-font);
      color: var(--odc-ink);
      -webkit-font-smoothing: antialiased;
      min-width: 360px;
    }
    caption {
      font-family: var(--odc-font-head);
      font-size: 15px;
      font-weight: 700;
      text-align: left;
      padding: 12px 16px 8px;
      color: var(--odc-ink);
      caption-side: top;
    }
    th {
      background: var(--odc-accent-soft);
      color: var(--odc-ink);
      font-weight: 700;
      text-align: left;
      padding: 10px 16px;
      font-size: 12px;
      letter-spacing: 0.03em;
      text-transform: uppercase;
      white-space: nowrap;
    }
    td {
      padding: 11px 16px;
      border-top: 1px solid var(--odc-border);
      color: var(--odc-ink);
      white-space: nowrap;
    }
    td.price {
      font-weight: 700;
      color: var(--odc-price);
      text-align: right;
      font-family: var(--odc-font-head);
      font-size: 15px;
    }
    tr.best td { background: var(--odc-accent-soft); }
    tr.best td.price { color: var(--odc-accent); }
    .badge {
      display: inline-block;
      background: var(--odc-accent);
      color: var(--odc-accent-ink, #fff);
      font-size: 10px;
      font-weight: 700;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      padding: 2px 7px;
      border-radius: 999px;
      margin-left: 8px;
      vertical-align: middle;
    }
    .empty {
      padding: 24px;
      text-align: center;
      color: var(--odc-muted);
      font-size: 14px;
    }
  `;

  @property({ type: Array }) rows?: PriceRow[];
  @property({ attribute: 'offer-price', type: Number }) offerPrice?: number;
  @property({ type: String }) currency = '€';
  @property({ type: String }) caption?: string;

  private _demoRows(base: number): PriceRow[] {
    return [
      { period: '02 юни – 09 юни',  board: 'Закуска',       price: base,       best: false },
      { period: '16 юни – 23 юни',  board: 'Закуска',       price: base + 80,  best: false },
      { period: '30 юни – 07 юли',  board: 'Полупансион',   price: base + 140, best: true  },
      { period: '14 юли – 21 юли',  board: 'Полупансион',   price: base + 210, best: false },
    ];
  }

  render() {
    const list = this.rows ?? (this.offerPrice != null ? this._demoRows(this.offerPrice) : []);

    if (list.length === 0) {
      return html`<div class="empty">Няма налични дати.</div>`;
    }

    const showOccupancy = list.some(r => r.occupancy);

    return html`
      <div class="wrap">
        <table part="table">
          ${this.caption ? html`<caption>${this.caption}</caption>` : nothing}
          <thead>
            <tr part="head">
              <th>Период</th>
              <th>Изхранване</th>
              ${showOccupancy ? html`<th>Заетост</th>` : nothing}
              <th style="text-align:right">Цена / човек</th>
            </tr>
          </thead>
          <tbody>
            ${list.map(r => html`
              <tr class=${r.best ? 'best' : ''} part="${r.best ? 'row best' : 'row'}">
                <td>${r.period}${r.best ? html`<span class="badge">Топ</span>` : nothing}</td>
                <td>${r.board}</td>
                ${showOccupancy ? html`<td>${r.occupancy ?? '—'}</td>` : nothing}
                <td class="price" part="price">${r.currency ?? this.currency}${r.price}</td>
              </tr>
            `)}
          </tbody>
        </table>
      </div>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'od-price-table': OdPriceTable;
  }
}
