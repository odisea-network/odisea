import { Component, ElementRef, effect, input, model, viewChild } from '@angular/core';
import { ButtonDirective } from '../button/button.directive';
import { Icon } from '../icon/icon';

/**
 * Side panel for create/edit forms: a native <dialog> docked to the right, so
 * focus trapping, Escape and the backdrop come from the platform. Bind
 * `[(open)]` to a signal; project the form as content and buttons as `[sheetActions]`.
 */
@Component({
  selector: 'ui-sheet',
  imports: [ButtonDirective, Icon],
  template: `
    <dialog #dialog class="sheet" [attr.aria-label]="title()" (close)="open.set(false)" (cancel)="$event.preventDefault(); open.set(false)">
      <header class="sheet__head">
        <div class="sheet__titles">
          <h2 class="sheet__title">{{ title() }}</h2>
          @if (sub(); as text) {
            <p class="sheet__sub">{{ text }}</p>
          }
        </div>
        <button type="button" uiButton="ghost" size="sm" iconOnly aria-label="Close" (click)="open.set(false)">
          <ui-icon name="x" />
        </button>
      </header>
      <div class="sheet__body"><ng-content /></div>
      <footer class="sheet__foot"><ng-content select="[sheetActions]" /></footer>
    </dialog>
  `,
  styleUrl: './sheet.scss',
})
export class Sheet {
  readonly open = model(false);
  readonly title = input.required<string>();
  readonly sub = input<string | null>(null);

  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  constructor() {
    effect(() => {
      const element = this.dialog().nativeElement;
      if (this.open() && !element.open) element.showModal();
      else if (!this.open() && element.open) element.close();
    });
  }
}
