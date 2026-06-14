import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { ApiService, CreatedWebhookSubscriptionDto, WebhookSubscriptionDto } from './api.service';
import { OdIcon } from './shared/od-icon';

const EVENTS: { id: string; label: string }[] = [
  { id: 'lead.created', label: 'Ново запитване' },
  { id: 'publication.published', label: 'Публикувана публикация' },
];

@Component({
  selector: 'app-webhooks-page',
  standalone: true,
  imports: [FormsModule, DatePipe, OdIcon],
  template: `
    <div class="od-page" style="max-width:1080px">
      <div class="od-pagehead">
        <div>
          <div class="od-eyebrow">Дистрибуция</div>
          <h1>Изходящи известия</h1>
          <p class="od-pagehead__sub">Абонаменти за webhook към вашия CRM или автоматизация. Изпращаме подписан POST при всяко събитие.</p>
        </div>
      </div>

      @if (error()) { <div class="od-notice od-notice--error" style="margin-top:18px"><od-icon name="alert" [size]="16" /><span>{{ error() }}</span></div> }

      @if (justCreated(); as c) {
        <div class="od-secret" style="margin-top:18px">
          <div class="secret-head"><od-icon name="alert" [size]="18" /><span>Запазете подписващия ключ сега</span></div>
          <p class="secret-text">Това е <b>единственият</b> път, в който показваме ключа. Съхранете го сигурно, няма да бъде показан отново.</p>
          <div class="secret-row">
            <code class="od-secret__code">{{ c.secret }}</code>
            <button type="button" class="od-btn od-btn--gold" (click)="copy(c.secret)"><od-icon [name]="copied() ? 'check' : 'copy'" [size]="16" />{{ copied() ? 'Копирано' : 'Копирай' }}</button>
          </div>
          <button type="button" class="secret-done" (click)="dismissSecret()"><od-icon name="check" [size]="14" />Запазих ключа</button>
        </div>
      }

      <div class="od-card" style="margin-top:18px;padding:20px 22px">
        <h3 class="block-title">Нов webhook</h3>
        <div class="form">
          <div>
            <label class="od-label">Адрес (само https)</label>
            <div class="od-input od-input--withicon"><od-icon name="link" [size]="16" /><input name="url" [(ngModel)]="url" placeholder="https://your-crm.bg/hooks/odisea" /></div>
          </div>
          <div>
            <label class="od-label">Типове събития</label>
            <div class="events">
              @for (ev of eventDefs; track ev.id) {
                <label class="event" [class.on]="selected.has(ev.id)">
                  <input type="checkbox" [checked]="selected.has(ev.id)" (change)="toggle(ev.id)" />
                  <div><div class="event__label">{{ ev.label }}</div><div class="event__hint od-mono">{{ ev.id }}</div></div>
                </label>
              }
            </div>
          </div>
          <div><button type="button" class="od-btn od-btn--primary" [disabled]="saving() || !url || selected.size === 0" (click)="create()"><od-icon name="plus" [size]="16" />{{ saving() ? 'Създаване…' : 'Създай webhook' }}</button></div>
        </div>
      </div>

      <div style="margin-top:26px">
        <div class="block-head">
          <h3 class="block-title" style="margin:0">Абонаменти</h3>
          <span class="muted">{{ subs().length }} крайни точки</span>
        </div>
        @if (loading()) {
          <p class="muted">Зареждане…</p>
        } @else if (subs().length === 0) {
          <div class="od-empty">
            <div class="od-empty__icon"><od-icon name="send" [size]="20" /></div>
            <h3>Все още нямате webhooks</h3>
            <p>Създайте първия си абонамент по-горе.</p>
          </div>
        } @else {
          <div class="od-tablewrap">
            @for (w of subs(); track w.id; let i = $index) {
              <div class="hook" [class.divider]="i > 0">
                <div class="hook__main">
                  <div class="hook__top">
                    <span class="hook__url od-mono">{{ w.url }}</span>
                    <span class="od-badge od-badge--sm" [class]="w.status === 'Active' ? 'od-badge--success' : 'od-badge--neutral'">{{ w.status === 'Active' ? 'Активен' : 'Спрян' }}</span>
                  </div>
                  <div class="hook__events">
                    @for (e of w.eventTypes.split(','); track e) { <span class="evchip od-mono">{{ e.trim() }}</span> }
                    <span class="muted" style="font-size:11.5px">· създаден {{ w.createdAt | date:'mediumDate' }}</span>
                  </div>
                </div>
                <div class="hook__actions">
                  @if (w.status === 'Active') {
                    <button type="button" class="od-btn od-btn--subtle od-btn--sm" (click)="setEnabled(w, false)"><od-icon name="pause" [size]="15" />Спри</button>
                  } @else {
                    <button type="button" class="od-btn od-btn--subtle od-btn--sm" (click)="setEnabled(w, true)"><od-icon name="play" [size]="15" />Активирай</button>
                  }
                  <button type="button" class="od-iconbtn" title="Изтрий" (click)="remove(w)"><od-icon name="trash" [size]="17" /></button>
                </div>
              </div>
            }
          </div>
        }
        <div class="verify">
          <od-icon name="shield" [size]="16" />
          <span>Проверявайте автентичността чрез хедъра <code class="od-mono">X-Odisea-Signature</code>, който е HMAC-SHA256 на тялото, подписан с вашия ключ.</span>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .muted { color: var(--od-500); }
    .block-title { font-size: 15px; font-weight: 700; color: var(--od-ink); }
    .block-head { display: flex; align-items: baseline; justify-content: space-between; margin-bottom: 12px; }
    .form { display: flex; flex-direction: column; gap: 16px; }
    .form .block-title { margin: 0 0 16px; }
    .events { display: flex; flex-direction: column; gap: 8px; }
    .event { display: flex; align-items: center; gap: 11px; padding: 11px 13px; border: 1px solid var(--od-border); border-radius: var(--od-r-md); cursor: pointer; background: var(--od-surface); transition: all var(--od-fast); }
    .event.on { border-color: var(--od-teal-600); background: var(--od-teal-50); }
    .event input { width: 16px; height: 16px; accent-color: var(--od-teal-600); }
    .event__label { font-size: 13.5px; font-weight: 600; color: var(--od-ink); }
    .event__hint { font-size: 11.5px; color: var(--od-500); }
    .secret-head { display: flex; align-items: center; gap: 9px; margin-bottom: 10px; color: var(--od-gold-700); font-size: 14.5px; font-weight: 700; }
    .secret-head span { color: var(--od-ink); }
    .secret-text { margin: 0 0 12px; font-size: 13px; color: var(--od-700); line-height: 1.55; }
    .secret-row { display: flex; align-items: center; gap: 10px; }
    .secret-done { margin-top: 12px; border: 0; background: transparent; color: var(--od-700); font-family: var(--od-font); font-size: 13px; font-weight: 600; cursor: pointer; display: inline-flex; align-items: center; gap: 5px; }
    .hook { display: flex; align-items: center; gap: 14px; padding: 14px 18px; }
    .hook.divider { border-top: 1px solid var(--od-border-2); }
    .hook__main { flex: 1; min-width: 0; }
    .hook__top { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
    .hook__url { font-size: 13px; font-weight: 600; color: var(--od-ink); overflow: hidden; text-overflow: ellipsis; }
    .hook__events { display: flex; align-items: center; gap: 6px; margin-top: 7px; flex-wrap: wrap; }
    .evchip { font-size: 11px; font-weight: 600; color: var(--od-teal-700); background: var(--od-teal-50); border-radius: 5px; padding: 2px 7px; }
    .hook__actions { display: flex; align-items: center; gap: 6px; }
    .verify { display: flex; align-items: flex-start; gap: 9px; margin-top: 14px; padding: 12px 15px; background: var(--od-50); border-radius: var(--od-r-md); font-size: 12.5px; color: var(--od-600); line-height: 1.6; }
    .verify code { background: var(--od-100); padding: 1px 6px; border-radius: 5px; color: var(--od-ink); }
  `],
})
export class WebhooksPage {
  private api = inject(ApiService);

  eventDefs = EVENTS;
  selected = new Set<string>([EVENTS[0].id]);
  url = '';

  subs = signal<WebhookSubscriptionDto[]>([]);
  loading = signal(true);
  saving = signal(false);
  error = signal<string | null>(null);
  justCreated = signal<CreatedWebhookSubscriptionDto | null>(null);
  copied = signal(false);

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

  copy(secret: string): void {
    try { navigator.clipboard?.writeText(secret); } catch { /* ignore */ }
    this.copied.set(true);
    setTimeout(() => this.copied.set(false), 1800);
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
