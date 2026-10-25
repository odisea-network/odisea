import { Injectable, signal } from '@angular/core';

export interface ToastAction {
  label: string;
  run: () => void;
}

export interface Toast {
  id: number;
  message: string;
  tone: 'ok' | 'info' | 'bad';
  action?: ToastAction;
}

const DEFAULT_TTL_MS = 5_000;

/** Short confirmations after an action ("Payment recorded · Balance €0.00"). */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 1;
  private readonly toastsSignal = signal<Toast[]>([]);
  readonly toasts = this.toastsSignal.asReadonly();

  show(message: string, options: { tone?: Toast['tone']; action?: ToastAction; ttlMs?: number } = {}): void {
    const toast: Toast = { id: this.nextId++, message, tone: options.tone ?? 'ok', action: options.action };
    this.toastsSignal.update(list => [...list, toast]);
    setTimeout(() => this.dismiss(toast.id), options.ttlMs ?? DEFAULT_TTL_MS);
  }

  dismiss(id: number): void {
    this.toastsSignal.update(list => list.filter(t => t.id !== id));
  }
}
