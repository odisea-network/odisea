import { Component, computed, input } from '@angular/core';

/** Thin bar for "paid of total" and "credit used" — the numbers always sit next to it. */
@Component({
  selector: 'ui-progress',
  template: `<i class="bar__fill" [style.width.%]="percent()"></i>`,
  styleUrl: './progress.scss',
  host: {
    role: 'progressbar',
    '[attr.aria-valuenow]': 'percent()',
    'aria-valuemin': '0',
    'aria-valuemax': '100',
    '[class.bar--warn]': 'tone() === "warn"',
  },
})
export class Progress {
  readonly value = input.required<number>();
  readonly max = input.required<number>();
  readonly tone = input<'brand' | 'warn'>('brand');

  protected readonly percent = computed(() => {
    const max = this.max();
    if (max <= 0) return 0;
    return Math.max(0, Math.min(100, Math.round((this.value() / max) * 100)));
  });
}
