import { Component, ElementRef, computed, effect, inject, signal, viewChild } from '@angular/core';
import { ButtonDirective } from '../button/button.directive';
import { Field } from '../field/field';
import { InputDirective } from '../input/input.directive';
import { DialogService } from './dialog.service';

/**
 * Mounted once in the app root. Uses the native <dialog> so focus trapping,
 * Escape and the backdrop come from the platform.
 */
@Component({
  selector: 'ui-dialog-host',
  imports: [ButtonDirective, Field, InputDirective],
  template: `
    <dialog #dialog class="dialog" (close)="onClose()" (cancel)="onCancel($event)" aria-labelledby="ui-dialog-title">
      @if (dialogs.pending()?.options; as options) {
        <form method="dialog" class="dialog__form" (submit)="onSubmit($event)">
          <div class="dialog__head">
            <h2 id="ui-dialog-title" class="dialog__title">{{ options.title }}</h2>
            <p class="dialog__text">{{ options.message }}</p>
          </div>
          @if (options.facts?.length) {
            <dl class="kv dialog__facts">
              @for (fact of options.facts; track fact.label) {
                <dt>{{ fact.label }}</dt>
                <dd>{{ fact.value }}</dd>
              }
            </dl>
          }
          @if (options.reason; as reason) {
            <ui-field for="ui-dialog-reason" [label]="reason.label + (reason.required ? ' (required)' : '')">
              <textarea
                uiInput
                name="reason"
                id="ui-dialog-reason"
                [placeholder]="reason.placeholder ?? ''"
                [value]="reasonText()"
                (input)="reasonText.set($any($event.target).value)"
              ></textarea>
            </ui-field>
          }
          <div class="dialog__actions">
            <button type="button" uiButton (click)="dismiss()">{{ options.cancelLabel ?? 'Cancel' }}</button>
            <button type="submit" [uiButton]="options.destructive ? 'danger' : 'primary'" [disabled]="!canConfirm()">
              {{ options.confirmLabel ?? 'Confirm' }}
            </button>
          </div>
        </form>
      }
    </dialog>
  `,
  styleUrl: './dialog-host.scss',
})
export class DialogHost {
  protected readonly dialogs = inject(DialogService);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  protected readonly reasonText = signal('');
  protected readonly canConfirm = computed(() => {
    const reason = this.dialogs.pending()?.options.reason;
    return !reason?.required || this.reasonText().trim().length > 0;
  });

  constructor() {
    effect(() => {
      const element = this.dialog().nativeElement;
      if (this.dialogs.pending()) {
        this.reasonText.set('');
        if (!element.open) element.showModal();
      } else if (element.open) {
        element.close();
      }
    });
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    if (!this.canConfirm()) return;
    this.dialogs.settle({ reason: this.reasonText().trim() });
  }

  protected dismiss(): void {
    this.dialogs.settle(null);
  }

  /** Escape key: let the platform close it, then report the dismissal. */
  protected onCancel(event: Event): void {
    event.preventDefault();
    this.dismiss();
  }

  protected onClose(): void {
    if (this.dialogs.pending()) this.dialogs.settle(null);
  }
}
