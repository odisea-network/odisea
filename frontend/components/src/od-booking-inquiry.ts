import { LitElement, html, css, nothing } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';

/** Payload emitted by the `od-inquiry-submit` event. */
export interface InquiryPayload {
  name: string;
  phone: string;
  email: string;
  message: string;
  offerId?: string;
  offerTitle?: string;
}

/**
 * Booking inquiry form. Not wired to the backend yet (Phase 4).
 * Emits `od-inquiry-submit` with the form payload on submit.
 *
 * @element od-booking-inquiry
 *
 * @attr {string} offer-title  - Offer title shown as sub-heading.
 * @attr {string} offer-id     - Offer ID included in the submitted payload.
 * @attr {string} [heading="Запитване за оферта"] - Form heading.
 * @attr {string} [submit-label="Изпрати запитване"] - Submit button label.
 *
 * @csspart form         - The `<form>` element.
 * @csspart heading      - The `<h4>` heading.
 * @csspart field        - Each `<input>` or `<textarea>`.
 * @csspart submit       - The submit `<button>`.
 * @csspart success-msg  - The post-submit success message.
 *
 * @fires od-inquiry-submit - Fired on form submission (before any backend call).
 *   `CustomEvent<InquiryPayload>`
 */
@customElement('od-booking-inquiry')
export class OdBookingInquiry extends LitElement {
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
      --odc-bg:          #ffffff;
      --odc-surface:     #ffffff;
      --odc-border:      rgba(20,30,28,0.12);
      --odc-radius:      14px;
      --odc-radius-sm:   9px;
      --odc-shadow:      0 1px 2px rgba(16,24,22,0.06);
    }
    *, *::before, *::after { box-sizing: border-box; }

    form {
      background: var(--odc-surface);
      border: 1px solid var(--odc-border);
      border-radius: var(--odc-radius);
      padding: 20px;
      box-shadow: var(--odc-shadow);
      font-family: var(--odc-font);
      color: var(--odc-ink);
      -webkit-font-smoothing: antialiased;
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    h4 {
      font-family: var(--odc-font-head);
      font-size: 18px;
      font-weight: 700;
      margin: 0;
      color: var(--odc-ink);
    }
    .sub {
      font-size: 13px;
      color: var(--odc-muted);
      margin: 0;
    }
    .row {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 10px;
    }
    @media (max-width: 480px) { .row { grid-template-columns: 1fr; } }

    input, textarea {
      width: 100%;
      padding: 0 12px;
      height: 42px;
      border: 1px solid var(--odc-border);
      border-radius: var(--odc-radius-sm);
      font-family: var(--odc-font);
      font-size: 13.5px;
      color: var(--odc-ink);
      background: var(--odc-bg);
      outline: none;
      transition: border-color 0.15s ease;
    }
    input:focus, textarea:focus { border-color: var(--odc-accent); }
    textarea { height: 80px; padding: 10px 12px; resize: vertical; }

    .required-note {
      font-size: 12px;
      color: var(--odc-muted);
      margin: 0;
    }
    .submit-btn {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 8px;
      height: 46px;
      background: var(--odc-accent);
      color: var(--odc-accent-ink);
      border: 0;
      border-radius: var(--odc-radius-sm);
      font-family: var(--odc-font);
      font-size: 14.5px;
      font-weight: 600;
      cursor: pointer;
      transition: filter 0.15s ease;
      width: 100%;
    }
    .submit-btn:hover { filter: brightness(1.06); }
    .submit-btn:disabled { opacity: 0.6; cursor: not-allowed; }
    .submit-btn svg { width: 16px; height: 16px; flex: none; }

    .success {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 10px;
      padding: 24px;
      text-align: center;
    }
    .success svg { width: 40px; height: 40px; color: var(--odc-accent); }
    .success strong { font-size: 16px; font-weight: 700; color: var(--odc-ink); }
    .success p { font-size: 13px; color: var(--odc-muted); margin: 0; }
  `;

  @property({ attribute: 'offer-title' }) offerTitle = '';
  @property({ attribute: 'offer-id' })   offerId = '';
  @property({ type: String }) heading = 'Запитване за оферта';
  @property({ attribute: 'submit-label' }) submitLabel = 'Изпрати запитване';

  @state() private _submitted = false;
  @state() private _submitting = false;

  // form field state
  @state() private _name = '';
  @state() private _phone = '';
  @state() private _email = '';
  @state() private _message = '';

  private async _submit(e: Event) {
    e.preventDefault();
    if (this._submitting) return;
    this._submitting = true;

    const payload: InquiryPayload = {
      name:       this._name.trim(),
      phone:      this._phone.trim(),
      email:      this._email.trim(),
      message:    this._message.trim(),
      offerId:    this.offerId || undefined,
      offerTitle: this.offerTitle || undefined,
    };

    this.dispatchEvent(new CustomEvent<InquiryPayload>('od-inquiry-submit', {
      detail: payload,
      bubbles: true,
      composed: true,
    }));

    // Simulate async — backend wiring is Phase 4
    await new Promise(r => setTimeout(r, 400));
    this._submitted = true;
    this._submitting = false;
  }

  private _arrowR() {
    return html`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12h14M12 5l7 7-7 7"/></svg>`;
  }
  private _check() {
    return html`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>`;
  }

  render() {
    if (this._submitted) {
      return html`
        <div class="success" part="success-msg">
          ${this._check()}
          <strong>Запитването е изпратено!</strong>
          <p>Ще се свържем с вас до 24 часа.</p>
        </div>
      `;
    }

    return html`
      <form part="form" @submit=${this._submit} novalidate>
        <h4 part="heading">${this.heading}</h4>
        ${this.offerTitle
          ? html`<p class="sub">${this.offerTitle} · отговаряме до 24 часа</p>`
          : nothing}

        <div class="row">
          <input
            part="field"
            type="text"
            placeholder="Име и фамилия *"
            required
            autocomplete="name"
            .value=${this._name}
            @input=${(e: InputEvent) => { this._name = (e.target as HTMLInputElement).value; }}
          />
          <input
            part="field"
            type="tel"
            placeholder="Телефон *"
            required
            autocomplete="tel"
            .value=${this._phone}
            @input=${(e: InputEvent) => { this._phone = (e.target as HTMLInputElement).value; }}
          />
        </div>

        <input
          part="field"
          type="email"
          placeholder="Имейл *"
          required
          autocomplete="email"
          .value=${this._email}
          @input=${(e: InputEvent) => { this._email = (e.target as HTMLInputElement).value; }}
        />

        <textarea
          part="field"
          placeholder="Брой пътници, период, въпроси…"
          .value=${this._message}
          @input=${(e: InputEvent) => { this._message = (e.target as HTMLTextAreaElement).value; }}
        ></textarea>

        <p class="required-note">* Задължителни полета</p>

        <button
          class="submit-btn"
          part="submit"
          type="submit"
          ?disabled=${this._submitting || !this._name || !this._phone || !this._email}
        >
          ${this._submitting ? 'Изпраща…' : html`${this.submitLabel}${this._arrowR()}`}
        </button>
      </form>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'od-booking-inquiry': OdBookingInquiry;
  }
}
