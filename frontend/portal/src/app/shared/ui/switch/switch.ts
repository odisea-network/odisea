import { Component, booleanAttribute, input, output } from '@angular/core';

/** On/off control for a setting that applies immediately (e.g. a rule's Active flag). */
@Component({
  selector: 'ui-switch',
  template: ``,
  styleUrl: './switch.scss',
  host: {
    role: 'switch',
    tabindex: '0',
    '[attr.aria-checked]': 'checked()',
    '[attr.aria-label]': 'label()',
    '[attr.aria-disabled]': 'disabled() ? "true" : null',
    '[class.sw--on]': 'checked()',
    '[class.sw--disabled]': 'disabled()',
    '(click)': 'toggle()',
    '(keydown.space)': '$event.preventDefault(); toggle()',
    '(keydown.enter)': 'toggle()',
  },
})
export class Switch {
  readonly checked = input.required<boolean>();
  readonly label = input.required<string>();
  readonly disabled = input(false, { transform: booleanAttribute });
  readonly checkedChange = output<boolean>();

  protected toggle(): void {
    if (!this.disabled()) this.checkedChange.emit(!this.checked());
  }
}
