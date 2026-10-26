import { Component, input, output } from '@angular/core';

export interface TabOption {
  value: string;
  label: string;
  count?: number | null;
}

/** Underlined section tabs inside a card. */
@Component({
  selector: 'ui-tabs',
  template: `
    @for (tab of tabs(); track tab.value) {
      <button
        type="button"
        role="tab"
        class="tab"
        [class.tab--on]="tab.value === value()"
        [attr.aria-selected]="tab.value === value()"
        (click)="valueChange.emit(tab.value)"
      >
        {{ tab.label }}
        @if (tab.count !== undefined && tab.count !== null) {
          <span class="tab__n">{{ tab.count }}</span>
        }
      </button>
    }
  `,
  styleUrl: './tabs.scss',
  host: { role: 'tablist' },
})
export class Tabs {
  readonly tabs = input.required<TabOption[]>();
  readonly value = input.required<string>();
  readonly valueChange = output<string>();
}
