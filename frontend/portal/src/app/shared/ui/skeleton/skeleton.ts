import { Component, computed, input } from '@angular/core';

const WIDTHS = ['60%', '90%', '75%', '82%', '55%'];

/** Loading placeholder: a few shimmering lines in place of content that is on its way. */
@Component({
  selector: 'ui-skeleton',
  template: `
    @for (width of bars(); track $index) {
      <span class="sk" [style.width]="width"></span>
    }
  `,
  styleUrl: './skeleton.scss',
  host: { role: 'status', 'aria-label': 'Loading' },
})
export class Skeleton {
  readonly lines = input(3);
  protected readonly bars = computed(() =>
    Array.from({ length: this.lines() }, (_, i) => WIDTHS[i % WIDTHS.length]));
}
