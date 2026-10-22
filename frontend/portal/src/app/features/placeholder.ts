import { Component, input } from '@angular/core';

/** Stand-in for feature pages that arrive in later slices. */
@Component({
  selector: 'app-placeholder',
  template: `
    <h2>{{ title() }}</h2>
    <p class="muted">This screen is on the roadmap — the API behind it is already live (see Swagger).</p>
  `,
  styles: `.muted { color: var(--text-muted); }`,
})
export class Placeholder {
  readonly title = input.required<string>();
}
