import { LitElement, html, css, nothing } from 'lit';
import { customElement, property } from 'lit/decorators.js';

/**
 * Full-width visual hero banner for a destination or featured offer.
 * Typically placed above an offer grid or carousel to set the scene.
 *
 * @element od-destination-hero
 *
 * @attr {string} image       - Background image URL.
 * @attr {string} eyebrow     - Small label above the title (e.g. flag + country name).
 * @attr {string} title       - Main hero heading.
 * @attr {string} subtitle    - Descriptive sub-line.
 * @attr {string} [cta-label] - Optional CTA button label.
 * @attr {string} [min-height="230px"] - Minimum block height.
 *
 * @csspart hero     - Root container `<div>`.
 * @csspart inner    - Inner content wrapper.
 * @csspart eyebrow  - Eyebrow `<span>`.
 * @csspart title    - Heading `<h2>`.
 * @csspart subtitle - Subtitle `<p>`.
 * @csspart cta      - CTA `<button>`.
 *
 * @slot - Default slot rendered inside the inner content area (after subtitle).
 *
 * @fires od-hero-cta - Fired when the CTA button is clicked. `CustomEvent<void>`
 */
@customElement('od-destination-hero')
export class OdDestinationHero extends LitElement {
  static styles = css`
    :host {
      display: block;
      --odc-font:       system-ui, sans-serif;
      --odc-font-head:  system-ui, sans-serif;
      --odc-accent:     #1a5a61;
      --odc-accent-ink: #ffffff;
      --odc-radius:     14px;
    }
    *, *::before, *::after { box-sizing: border-box; }

    .hero {
      position: relative;
      border-radius: var(--odc-radius);
      overflow: hidden;
      min-height: var(--_min-h, 230px);
      display: flex;
      align-items: flex-end;
      padding: 28px 28px 28px;
      color: #fff;
      -webkit-font-smoothing: antialiased;
    }
    .hero-img {
      position: absolute;
      inset: 0;
      width: 100%;
      height: 100%;
      object-fit: cover;
      display: block;
    }
    /* gradient scrim — bottom-heavy for legibility */
    .hero::after {
      content: "";
      position: absolute;
      inset: 0;
      background: linear-gradient(0deg, rgba(0,0,0,0.68) 0%, rgba(0,0,0,0.05) 65%);
      pointer-events: none;
    }
    .inner {
      position: relative;
      z-index: 1;
      max-width: 600px;
    }
    .eyebrow {
      font-family: var(--odc-font);
      font-size: 12px;
      font-weight: 700;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      opacity: 0.9;
      display: block;
      margin-bottom: 8px;
    }
    h2.title {
      font-family: var(--odc-font-head);
      font-size: clamp(24px, 3.5vw, 36px);
      font-weight: 700;
      margin: 0 0 8px;
      line-height: 1.1;
    }
    p.subtitle {
      font-family: var(--odc-font);
      font-size: 15px;
      opacity: 0.92;
      max-width: 480px;
      margin: 0 0 16px;
      line-height: 1.5;
    }
    .cta {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      height: 44px;
      padding: 0 20px;
      background: var(--odc-accent);
      color: var(--odc-accent-ink);
      border: 0;
      border-radius: 9px;
      font-family: var(--odc-font);
      font-size: 14px;
      font-weight: 600;
      cursor: pointer;
      transition: filter 0.15s ease;
    }
    .cta:hover { filter: brightness(1.1); }
    .cta svg { width: 16px; height: 16px; flex: none; }
  `;

  @property({ type: String }) image = '';
  @property({ type: String }) eyebrow = '';
  @property({ type: String }) title = '';
  @property({ type: String }) subtitle = '';
  @property({ attribute: 'cta-label' }) ctaLabel = '';
  @property({ attribute: 'min-height' }) minHeight = '230px';

  updated(changed: Map<PropertyKey, unknown>) {
    if (changed.has('minHeight')) {
      this.style.setProperty('--_min-h', this.minHeight);
    }
  }

  private _onCta() {
    this.dispatchEvent(new CustomEvent('od-hero-cta', { bubbles: true, composed: true }));
  }

  private _arrowR() {
    return html`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12h14M12 5l7 7-7 7"/></svg>`;
  }

  render() {
    return html`
      <div class="hero" part="hero">
        ${this.image ? html`<img class="hero-img" src=${this.image} alt=${this.title} />` : nothing}
        <div class="inner" part="inner">
          ${this.eyebrow ? html`<span class="eyebrow" part="eyebrow">${this.eyebrow}</span>` : nothing}
          ${this.title   ? html`<h2 class="title" part="title">${this.title}</h2>`          : nothing}
          ${this.subtitle ? html`<p class="subtitle" part="subtitle">${this.subtitle}</p>`   : nothing}
          ${this.ctaLabel ? html`
            <button class="cta" part="cta" @click=${this._onCta}>
              ${this.ctaLabel}${this._arrowR()}
            </button>` : nothing}
          <slot></slot>
        </div>
      </div>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'od-destination-hero': OdDestinationHero;
  }
}
