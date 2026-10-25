import { Component, booleanAttribute, input, output } from '@angular/core';

export interface SegmentOption<T extends string = string> {
  value: T;
  label: string;
  count?: number | null;
}

/** Exclusive choice between a few options: saved views, card/table, payment method. */
@Component({
  selector: 'ui-segmented',
  template: `
    @for (option of options(); track option.value) {
      <button
        type="button"
        class="seg__btn"
        [class.seg__btn--on]="option.value === value()"
        [attr.aria-pressed]="option.value === value()"
        (click)="valueChange.emit(option.value)"
      >
        {{ option.label }}
        @if (option.count !== undefined && option.count !== null) {
          <span class="seg__n">{{ option.count }}</span>
        }
      </button>
    }
  `,
  styleUrl: './segmented.scss',
  host: { role: 'group', '[class.seg--stretch]': 'stretch()' },
})
export class Segmented {
  readonly options = input.required<SegmentOption[]>();
  readonly value = input.required<string>();
  readonly stretch = input(false, { transform: booleanAttribute });
  readonly valueChange = output<string>();
}
