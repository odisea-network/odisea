import { Component, booleanAttribute, input } from '@angular/core';
import { Icon } from '../icon/icon';

/**
 * Label + control + hint/error. Pass the control's id as `for` so the label is
 * associated explicitly; the control itself is projected.
 */
@Component({
  selector: 'ui-field',
  imports: [Icon],
  template: `
    <div class="field">
      <label class="field__label" [attr.for]="for()">
        {{ label() }}
        @if (optional()) {
          <span class="field__optional">(optional)</span>
        }
      </label>
      <ng-content />
      @if (error(); as message) {
        <span class="field__error" role="alert">
          <ui-icon name="alert-circle" size="sm" />{{ message }}
        </span>
      } @else if (hint(); as text) {
        <span class="field__hint">{{ text }}</span>
      }
    </div>
  `,
  styleUrl: './field.scss',
})
export class Field {
  readonly label = input.required<string>();
  /** id of the projected control. */
  readonly for = input<string | null>(null);
  readonly hint = input<string | null>(null);
  readonly error = input<string | null>(null);
  readonly optional = input(false, { transform: booleanAttribute });
}
