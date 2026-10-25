import { Directive, booleanAttribute, input } from '@angular/core';

/**
 * Skin for native form controls: `<input uiInput>`, `<select uiInput>`, `<textarea uiInput>`.
 * Styles live in styles/primitives/_input.scss.
 */
@Directive({
  selector: 'input[uiInput], select[uiInput], textarea[uiInput]',
  host: {
    class: 'ui-inp',
    '[class.ui-inp--sm]': 'size() === "sm"',
    '[class.ui-inp--error]': 'invalid()',
    '[attr.aria-invalid]': 'invalid() ? "true" : null',
  },
})
export class InputDirective {
  readonly size = input<'sm' | 'md'>('md');
  readonly invalid = input(false, { transform: booleanAttribute });
}
