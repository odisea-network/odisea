import { Component, computed, input } from '@angular/core';
import { ICONS, IconName } from './icons';

export type IconSize = 'sm' | 'md' | 'lg';

/** Inline stroke icon. Decorative by default; wrap in a labelled control for meaning. */
@Component({
  selector: 'ui-icon',
  template: `<svg viewBox="0 0 24 24" aria-hidden="true" focusable="false"><path [attr.d]="path()" /></svg>`,
  styleUrl: './icon.scss',
  host: { '[class]': '"ui-icon ui-icon--" + size()' },
})
export class Icon {
  readonly name = input.required<IconName>();
  readonly size = input<IconSize>('md');

  protected readonly path = computed(() => ICONS[this.name()]);
}
