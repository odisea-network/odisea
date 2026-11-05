import { Injectable, signal } from '@angular/core';

const PHONE = '(max-width: 599px)';

/** Which layout a screen should pick when CSS alone is not enough (table vs card list). */
@Injectable({ providedIn: 'root' })
export class ViewportService {
  readonly isPhone = signal(false);

  constructor() {
    // jsdom (unit tests) has no matchMedia; everything then renders the wide layout.
    if (typeof matchMedia !== 'function') return;
    const query = matchMedia(PHONE);
    this.isPhone.set(query.matches);
    query.addEventListener('change', event => this.isPhone.set(event.matches));
  }
}
