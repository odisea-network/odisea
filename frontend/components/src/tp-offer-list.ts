import { LitElement, html, css, nothing } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';

interface OfferDto {
  id: string;
  title: string;
  country: string;
  city: string;
  price: number;
  currency: string;
  boardBasis: string;
  transport: string;
  durationNights: number;
  imageUrl: string;
}

interface PublicationManifest {
  key: string;
  version: number;
  status: string;
  collectionId: string;
  collectionSlug: string;
  collectionName: string;
  offersUrl: string;
  themeId: string | null;
  experience: ExperienceConfig | null;
  etag: string;
}

interface ExperienceConfig {
  type: string;
  columns: number;
  cardStyle: string;
  showPrice: boolean;
  inquiry: boolean;
  openNewTab: boolean;
}

@customElement('tp-offer-list')
export class TpOfferList extends LitElement {
  static styles = css`
    :host {
      display: block;
      font-family: var(--tp-font, system-ui, sans-serif);
      color: var(--tp-color-text, #1a1a1a);
      background: var(--tp-color-bg, transparent);
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
      gap: 16px;
    }
    .card {
      background: var(--tp-color-bg, #fff);
      border-radius: var(--tp-radius, 8px);
      overflow: hidden;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
      display: flex;
      flex-direction: column;
    }
    .card img {
      width: 100%;
      height: 160px;
      object-fit: cover;
      display: block;
    }
    .body { padding: 12px 14px 14px; }
    .title { font-weight: 600; font-size: 1.05rem; margin: 0 0 4px; }
    .loc { font-size: 0.85rem; opacity: 0.7; margin: 0 0 8px; }
    .meta { display: flex; gap: 10px; font-size: 0.8rem; opacity: 0.8; margin-bottom: 8px; flex-wrap: wrap; }
    .meta span { background: rgba(0,0,0,0.05); padding: 2px 8px; border-radius: 999px; }
    .price {
      font-weight: 700;
      color: var(--tp-color-primary, #0066cc);
      font-size: 1.1rem;
    }
    .status { padding: 12px; text-align: center; opacity: 0.7; }
    .error { color: #c00; }
  `;

  /** Stable public publication key — preferred over collection=. */
  @property({ type: String }) publication?: string;

  /**
   * @deprecated Use publication= instead. Collection slug support will be
   * removed in a future major version.
   */
  @property({ type: String }) collection?: string;

  @property({ type: String }) endpoint?: string;
  @property({ type: String }) layout: 'grid' | 'carousel' = 'grid';
  @property({ type: String, attribute: 'api-base' }) apiBase = '';

  @state() private offers: OfferDto[] = [];
  @state() private loading = true;
  @state() private error: string | null = null;

  connectedCallback() {
    super.connectedCallback();
    this.load();
  }

  private async load() {
    try {
      if (this.publication) {
        await this.loadViaManifest(this.publication);
      } else {
        if (this.collection) {
          console.warn(
            'tp-offer-list: the collection= attribute is deprecated. ' +
            'Use publication= with a publication key from the Odisea platform instead.',
          );
        }
        const url = this.legacyUrl;
        if (!url) {
          this.error = 'tp-offer-list: provide either "publication" or "collection"';
          return;
        }
        await this.fetchOffers(url);
      }
    } finally {
      this.loading = false;
    }
  }

  private async loadViaManifest(key: string) {
    const manifestUrl = `${this.apiBase}/api/v1/publications/${key}`;
    const res = await fetch(manifestUrl);
    if (!res.ok) {
      this.error = `Publication "${key}" not found (HTTP ${res.status})`;
      return;
    }
    const manifest: PublicationManifest = await res.json();
    await this.fetchOffers(`${this.apiBase}${manifest.offersUrl}`);
  }

  private async fetchOffers(url: string) {
    const r = await fetch(url);
    if (!r.ok) throw new Error(`HTTP ${r.status}`);
    this.offers = await r.json();
  }

  private get legacyUrl(): string {
    if (this.endpoint) return this.endpoint;
    if (this.collection) return `${this.apiBase}/api/v1/collections/${this.collection}/offers`;
    return '';
  }

  private formatPrice(o: OfferDto) {
    return `${o.price.toLocaleString()} ${o.currency}`;
  }

  private boardLabel(b: string) {
    return ({
      RoomOnly: 'Room only',
      BedAndBreakfast: 'B&B',
      HalfBoard: 'Half board',
      FullBoard: 'Full board',
      AllInclusive: 'All inclusive',
    } as Record<string, string>)[b] ?? b;
  }

  render() {
    if (this.loading) return html`<div class="status">Loading offers…</div>`;
    if (this.error) return html`<div class="status error">${this.error}</div>`;
    if (this.offers.length === 0) return html`<div class="status">No offers found.</div>`;

    return html`
      <div class="grid">
        ${this.offers.map(
          (o) => html`
            <article class="card">
              ${o.imageUrl
                ? html`<img src="${o.imageUrl}" alt="${o.title}" loading="lazy" />`
                : nothing}
              <div class="body">
                <h3 class="title">${o.title}</h3>
                <p class="loc">${o.city}, ${o.country}</p>
                <div class="meta">
                  <span>${o.durationNights} nights</span>
                  <span>${this.boardLabel(o.boardBasis)}</span>
                </div>
                <div class="price">${this.formatPrice(o)}</div>
              </div>
            </article>
          `,
        )}
      </div>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'tp-offer-list': TpOfferList;
  }
}
