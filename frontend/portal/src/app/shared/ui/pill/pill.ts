import { Component, input } from '@angular/core';

export type PillTone = 'neutral' | 'ok' | 'warn' | 'bad' | 'brand';

/** Small label for secondary facts (availability, roles, document status). */
@Component({
  selector: 'ui-pill',
  template: `<ng-content />`,
  styleUrl: './pill.scss',
  host: { '[class]': '"pill pill--" + tone()' },
})
export class Pill {
  readonly tone = input<PillTone>('neutral');
}
