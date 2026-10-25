import { Directive, booleanAttribute, input } from '@angular/core';

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger' | 'danger-quiet';
export type ButtonSize = 'sm' | 'md' | 'lg';

/**
 * `<button uiButton="primary">` / `<a uiButton routerLink>`. Native elements keep
 * their semantics and keyboard behaviour; the directive only applies the skin.
 * Styles live in styles/primitives/_button.scss.
 */
@Directive({
  selector: 'button[uiButton], a[uiButton]',
  host: {
    class: 'ui-btn',
    '[class.ui-btn--primary]': 'variant() === "primary"',
    '[class.ui-btn--secondary]': 'variant() === "secondary"',
    '[class.ui-btn--ghost]': 'variant() === "ghost"',
    '[class.ui-btn--danger]': 'variant() === "danger"',
    '[class.ui-btn--danger-quiet]': 'variant() === "danger-quiet"',
    '[class.ui-btn--sm]': 'size() === "sm"',
    '[class.ui-btn--lg]': 'size() === "lg"',
    '[class.ui-btn--block]': 'block()',
    '[class.ui-btn--icon]': 'iconOnly()',
  },
})
export class ButtonDirective {
  readonly variant = input<ButtonVariant, ButtonVariant | ''>('secondary', {
    alias: 'uiButton',
    transform: value => value || 'secondary',
  });
  readonly size = input<ButtonSize>('md');
  readonly block = input(false, { transform: booleanAttribute });
  /** Square icon-only button: give it an aria-label. */
  readonly iconOnly = input(false, { transform: booleanAttribute });
}
