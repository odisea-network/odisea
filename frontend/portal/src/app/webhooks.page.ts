import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { ApiService, CreatedWebhookSubscriptionDto, WebhookSubscriptionDto } from './api.service';

const EVENTS = ['lead.created', 'publication.published'];

@Component({
  selector: 'app-webhooks-page',
  standalone: true,
  imports: [FormsModule, DatePipe],
  template: `
    <h2>Webhooks</h2>
    <p class="muted">Receive a signed POST when events happen in your account. See the docs for signature verification.</p>
    @if (error()) { <p class="error">{{ error() }}</p> }

    @if (justCreated(); as c) {
      <div class="secret-card">
        <strong>Subscription created.</strong> Copy your signing secret now — it won't be shown again.
        <code class="secret">{{ c.secret }}</code>
        <button type="button" (click)="dismissSecret()">Done</button>
      </div>
    }

    <section class="form-card">
      <h3>New subscription</h3>
      <label>Endpoint URL
        <input name="url" [(ngModel)]="url" placeholder="https://your-crm.example/odisea-hook" />
      </label>
      <div class="events">
        <span class="events-label">Events</span>
        @for (e of events; track e) {
          <label class="chk"><input type="checkbox" [checked]="selected.has(e)" (change)="toggle(e)" /> {{ e }}</label>
        }
      </div>
      <button type="button" (click)="create()" [disabled]="saving() || !url || selected.size === 0">
        {{ saving() ? 'Creating…' : 'Create subscription' }}
      </button>
    </section>

    @if (loading()) {
      <p class="muted">Loading…</p>
    } @else {
      <table>
        <thead>
          <tr><th>Endpoint</th><th>Events</th><th>Status</th><th>Created</th><th></th></tr>
        </thead>
        <tbody>
          @for (w of subs(); track w.id) {
            <tr>
              <td class="url">{{ w.url }}</td>
              <td>{{ w.eventTypes }}</td>
              <td><span class="status" [class.active]="w.status === 'Active'">{{ w.status }}</span></td>
              <td>{{ w.createdAt | date:'short' }}</td>
              <td class="row-actions">
                @if (w.status === 'Active') {
                  <button type="button" (click)="setEnabled(w, false)">Disable</button>
                } @else {
                  <button type="button" (click)="setEnabled(w, true)">Enable</button>
                }
                <button type="button" class="danger" (click)="remove(w)">Delete</button>
              </td>
            </tr>
          } @empty {
            <tr><td colspan="5" class="muted">No subscriptions yet.</td></tr>
          }
        </tbody>
      </table>
    }
  `,
  styles: [`
    .form-card { border: 1px solid #e3e3e3; border-radius: 8px; padding: 16px; margin: 16px 0 24px; background: #fafbfc; }
    .form-card h3 { margin: 0 0 12px; font-size: 1rem; }
    label { display: flex; flex-direction: column; gap: 4px; font-size: 0.85rem; color: #555; }
    input[type=text], input:not([type]) { padding: 8px 10px; border: 1px solid #ccc; border-radius: 6px; font: inherit; }
    .events { display: flex; align-items: center; gap: 14px; margin: 12px 0; flex-wrap: wrap; }
    .events-label { font-size: 0.85rem; color: #555; }
    .chk { flex-direction: row; align-items: center; gap: 5px; }
    button { padding: 8px 14px; border: 0; border-radius: 6px; background: #0a2540; color: #fff; cursor: pointer; }
    button:disabled { opacity: 0.5; cursor: default; }
    .secret-card { border: 1px solid #f0c36d; background: #fff8e6; border-radius: 8px; padding: 14px; margin-bottom: 16px; }
    .secret { display: block; margin: 8px 0; padding: 8px; background: #1c1c1c; color: #9ef; border-radius: 6px; word-break: break-all; font-family: monospace; }
    table { width: 100%; border-collapse: collapse; }
    th, td { padding: 9px 10px; border-bottom: 1px solid #eee; text-align: left; }
    th { font-weight: 600; background: #fafafa; }
    .url { font-family: monospace; font-size: 0.85rem; }
    .row-actions { display: flex; gap: 8px; }
    .row-actions button { padding: 4px 10px; font-size: 0.85rem; background: #335; }
    .row-actions button.danger { background: #b3322c; }
    .status { font-size: 0.8rem; color: #999; }
    .status.active { color: #186; }
    .muted { color: #888; }
    .error { color: #c00; }
  `],
})
export class WebhooksPage {
  private api = inject(ApiService);

  events = EVENTS;
  selected = new Set<string>([EVENTS[0]]);
  url = '';

  subs = signal<WebhookSubscriptionDto[]>([]);
  loading = signal(true);
  saving = signal(false);
  error = signal<string | null>(null);
  justCreated = signal<CreatedWebhookSubscriptionDto | null>(null);

  constructor() {
    this.reload();
  }

  toggle(e: string): void {
    if (this.selected.has(e)) this.selected.delete(e);
    else this.selected.add(e);
  }

  create(): void {
    this.saving.set(true);
    this.error.set(null);
    this.api.createWebhook(this.url, [...this.selected].join(',')).subscribe({
      next: (created) => {
        this.justCreated.set(created);
        this.url = '';
        this.saving.set(false);
        this.reload();
      },
      error: (e) => { this.error.set(e?.error?.detail ?? 'Create failed'); this.saving.set(false); },
    });
  }

  dismissSecret(): void {
    this.justCreated.set(null);
  }

  setEnabled(w: WebhookSubscriptionDto, enabled: boolean): void {
    const op = enabled ? this.api.enableWebhook(w.id) : this.api.disableWebhook(w.id);
    op.subscribe({ next: () => this.reload(), error: (e) => this.error.set(e?.error?.detail ?? 'Update failed') });
  }

  remove(w: WebhookSubscriptionDto): void {
    this.api.deleteWebhook(w.id).subscribe({ next: () => this.reload(), error: (e) => this.error.set(e?.error?.detail ?? 'Delete failed') });
  }

  private reload(): void {
    this.loading.set(true);
    this.api.listWebhooks().subscribe({
      next: (xs) => { this.subs.set(xs); this.loading.set(false); },
      error: (e) => { this.error.set(e?.error?.detail ?? 'Failed to load'); this.loading.set(false); },
    });
  }
}
