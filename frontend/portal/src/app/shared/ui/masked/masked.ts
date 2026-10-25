import { Component, computed, input, signal } from '@angular/core';
import { ButtonDirective } from '../button/button.directive';
import { Icon } from '../icon/icon';

/**
 * Sensitive identifier (travel document number) shown masked to its last digits,
 * revealed only on an explicit click. The full value is already on the client;
 * this keeps it off screens, screenshots and shoulder-surfers by default.
 */
@Component({
  selector: 'ui-masked',
  imports: [ButtonDirective, Icon],
  template: `
    <span class="mask">{{ shown() }}</span>
    @if (value()) {
      <button
        type="button"
        uiButton="ghost"
        size="sm"
        iconOnly
        (click)="revealed.set(!revealed())"
        [attr.aria-label]="(revealed() ? 'Hide ' : 'Reveal ') + subject()"
        [attr.aria-pressed]="revealed()"
      >
        <ui-icon [name]="revealed() ? 'eye-off' : 'eye'" />
      </button>
    }
  `,
  styleUrl: './masked.scss',
})
export class Masked {
  readonly value = input.required<string | null | undefined>();
  /** What is being revealed, for the button label: "passport number for Maria Georgieva". */
  readonly subject = input.required<string>();
  readonly visibleDigits = input(4);

  protected readonly revealed = signal(false);

  protected readonly shown = computed(() => {
    const value = this.value();
    if (!value) return '—';
    if (this.revealed()) return value;
    const keep = Math.min(this.visibleDigits(), Math.max(0, value.length - 1));
    return `${'•'.repeat(Math.max(4, value.length - keep))} ${value.slice(value.length - keep)}`;
  });
}
