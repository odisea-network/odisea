import { Component, inject } from '@angular/core';
import { Icon } from '../icon/icon';
import { ToastService } from './toast.service';

/** Mounted once in the app root; renders the ToastService queue. */
@Component({
  selector: 'ui-toast-host',
  imports: [Icon],
  template: `
    @for (toast of toasts.toasts(); track toast.id) {
      <div class="toast" [class]="'toast toast--' + toast.tone">
        <ui-icon [name]="toast.tone === 'bad' ? 'alert-circle' : toast.tone === 'info' ? 'info' : 'check'" />
        <span class="toast__text">{{ toast.message }}</span>
        @if (toast.action; as action) {
          <button type="button" class="toast__action" (click)="action.run(); toasts.dismiss(toast.id)">
            {{ action.label }}
          </button>
        }
        <button type="button" class="toast__close" aria-label="Dismiss" (click)="toasts.dismiss(toast.id)">
          <ui-icon name="x" size="sm" />
        </button>
      </div>
    }
  `,
  styleUrl: './toast-host.scss',
  host: { role: 'status', 'aria-live': 'polite' },
})
export class ToastHost {
  protected readonly toasts = inject(ToastService);
}
