import { Injectable, signal } from '@angular/core';

export interface ConfirmOptions {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  /** Red confirm button for irreversible actions. */
  destructive?: boolean;
  /** Ask for a free-text reason; `required` blocks confirm until it is filled. */
  reason?: { label: string; placeholder?: string; required: boolean };
  /** Facts worth restating before deciding (paid amount, supplier hold…). */
  facts?: { label: string; value: string }[];
}

export interface ConfirmResult {
  reason: string;
}

interface PendingConfirm {
  options: ConfirmOptions;
  resolve: (result: ConfirmResult | null) => void;
}

/**
 * Replaces window.confirm / window.prompt with an accessible modal that can
 * restate the stakes and collect a reason. `confirm()` resolves with the reason
 * (possibly empty) or null when dismissed.
 */
@Injectable({ providedIn: 'root' })
export class DialogService {
  private readonly pendingSignal = signal<PendingConfirm | null>(null);
  readonly pending = this.pendingSignal.asReadonly();

  confirm(options: ConfirmOptions): Promise<ConfirmResult | null> {
    // One at a time: a second request while one is open dismisses the first.
    this.pendingSignal()?.resolve(null);
    return new Promise(resolve => this.pendingSignal.set({ options, resolve }));
  }

  /** Called by the host when the user decides. */
  settle(result: ConfirmResult | null): void {
    const pending = this.pendingSignal();
    this.pendingSignal.set(null);
    pending?.resolve(result);
  }
}
