import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ApiService, LeadDto } from './api.service';
import { OdIcon } from './shared/od-icon';

const STATUSES = ['New', 'Contacted', 'Converted', 'Closed'];
const STATUS_META: Record<string, { tone: string; label: string }> = {
  New: { tone: 'info', label: 'Нов' },
  Contacted: { tone: 'warning', label: 'Свързан' },
  Converted: { tone: 'success', label: 'Реализиран' },
  Closed: { tone: 'neutral', label: 'Затворен' },
};
const FILTERS: [string | null, string][] = [
  [null, 'Всички'], ['New', 'Нови'], ['Contacted', 'Свързани'], ['Converted', 'Реализирани'], ['Closed', 'Затворени'],
];

@Component({
  selector: 'app-leads-page',
  standalone: true,
  imports: [DatePipe, OdIcon],
  template: `
    <div class="od-page">
      <div class="od-pagehead">
        <div>
          <div class="od-eyebrow">Резултати</div>
          <h1>Запитвания</h1>
          <p class="od-pagehead__sub">Входяща кутия за запитвания и заявки от вградените компоненти по сайтовете ви.</p>
        </div>
      </div>

      <div class="od-stats" style="margin-top:22px;margin-bottom:20px">
        <div class="od-stat"><div class="od-stat__top"><span class="od-stat__label">Общо</span><span class="od-stat__icon"><od-icon name="users" [size]="16" /></span></div><div class="od-stat__value">{{ leads().length }}</div></div>
        <div class="od-stat"><div class="od-stat__top"><span class="od-stat__label">Нови</span><span class="od-stat__icon"><od-icon name="bell" [size]="16" /></span></div><div class="od-stat__value">{{ count('New') }}</div></div>
        <div class="od-stat"><div class="od-stat__top"><span class="od-stat__label">Свързани</span><span class="od-stat__icon"><od-icon name="mail" [size]="16" /></span></div><div class="od-stat__value">{{ count('Contacted') }}</div></div>
        <div class="od-stat"><div class="od-stat__top"><span class="od-stat__label">Реализирани</span><span class="od-stat__icon gold"><od-icon name="check" [size]="16" /></span></div><div class="od-stat__value">{{ count('Converted') }}</div></div>
      </div>

      @if (error()) { <div class="od-notice od-notice--error"><od-icon name="alert" [size]="16" /><span>{{ error() }}</span></div> }

      <div class="filters">
        @for (f of filters; track f[1]) {
          <button type="button" class="od-chip" [class.od-chip--on]="filter() === f[0]" (click)="setFilter(f[0])">
            {{ f[1] }}<span class="od-chip__count">{{ f[0] === null ? leads().length : count(f[0]) }}</span>
          </button>
        }
      </div>

      @if (loading()) {
        <p class="muted">Зареждане…</p>
      } @else if (leads().length === 0) {
        <div class="od-empty">
          <div class="od-empty__icon"><od-icon name="bell" [size]="22" /></div>
          <h3>Няма запитвания</h3>
          <p>В тази категория няма запитвания в момента.</p>
        </div>
      } @else {
        <div class="od-tablewrap">
          <table class="od-table">
            <thead><tr><th>Получено</th><th>Контакт</th><th>Тип</th><th>Съобщение</th><th>Статус</th><th>Промени</th></tr></thead>
            <tbody>
              @for (l of leads(); track l.id) {
                <tr class="clickable" (click)="open(l)">
                  <td style="color:var(--od-500);white-space:nowrap">{{ l.createdAt | date:'short' }}</td>
                  <td>
                    <div style="font-weight:600;color:var(--od-ink)">{{ l.contactName }}</div>
                    <div style="font-size:12px;color:var(--od-500)">{{ l.contactEmail }}</div>
                  </td>
                  <td>
                    <span class="od-badge od-badge--sm" [class]="l.kind === 'BookingRequest' ? 'od-badge--teal' : 'od-badge--neutral'">{{ l.kind === 'BookingRequest' ? 'Заявка' : 'Запитване' }}</span>
                    @if (l.kind === 'BookingRequest') { <div class="od-num" style="font-size:11.5px;color:var(--od-500);margin-top:3px">{{ l.partySize }} души · {{ l.nights }} нощ.</div> }
                  </td>
                  <td class="msg">{{ l.message || '—' }}</td>
                  <td><span class="od-badge od-badge--sm" [class]="'od-badge--' + meta(l.status).tone">{{ meta(l.status).label }}</span></td>
                  <td (click)="$event.stopPropagation()">
                    <select class="od-input" style="height:32px;width:auto;font-size:12.5px;font-weight:600" [value]="l.status" (change)="advance(l, $any($event.target).value)">
                      @for (s of statuses; track s) { <option [value]="s">{{ meta(s).label }}</option> }
                    </select>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>

    @if (opened(); as l) {
      <div class="od-drawer-overlay" (click)="openId.set(null)">
        <div class="od-drawer" (click)="$event.stopPropagation()">
          <div class="od-drawer__head">
            <div>
              <div style="display:flex;gap:8px;margin-bottom:8px">
                <span class="od-badge od-badge--sm" [class]="l.kind === 'BookingRequest' ? 'od-badge--teal' : 'od-badge--neutral'">{{ l.kind === 'BookingRequest' ? 'Заявка за резервация' : 'Запитване' }}</span>
                <span class="od-badge od-badge--sm" [class]="'od-badge--' + meta(l.status).tone">{{ meta(l.status).label }}</span>
              </div>
              <h2 style="margin:0;font-size:19px;font-weight:700;color:var(--od-ink)">{{ l.contactName }}</h2>
              <div style="font-size:12.5px;color:var(--od-500);margin-top:3px">{{ l.createdAt | date:'medium' }} · {{ l.publicationKey }}</div>
            </div>
            <button type="button" class="od-iconbtn" (click)="openId.set(null)"><od-icon name="x" [size]="18" /></button>
          </div>
          <div class="od-drawer__body">
            <div>
              <div class="od-eyebrow" style="margin-bottom:8px">Контакт</div>
              <a [href]="'mailto:' + l.contactEmail" class="contact-link"><od-icon name="mail" [size]="15" />{{ l.contactEmail }}</a>
              @if (l.contactPhone) { <span class="contact-row"><od-icon name="smartphone" [size]="15" />{{ l.contactPhone }}</span> }
            </div>
            @if (l.kind === 'BookingRequest') {
              <div style="display:flex;gap:10px">
                <div class="metric"><div class="metric__l">Пътници</div><div class="metric__v od-num">{{ l.partySize }}</div></div>
                <div class="metric"><div class="metric__l">Нощувки</div><div class="metric__v od-num">{{ l.nights }}</div></div>
              </div>
            }
            <div>
              <div class="od-eyebrow" style="margin-bottom:8px">Съобщение</div>
              <p class="msg-box">{{ l.message || 'Без съобщение.' }}</p>
            </div>
            <div>
              <div class="od-eyebrow" style="margin-bottom:8px">Канал</div>
              <div style="font-size:13.5px;color:var(--od-700)">{{ l.channel }}</div>
            </div>
          </div>
          <div class="od-drawer__foot">
            <span style="font-size:13px;color:var(--od-600)">Промени статус:</span>
            <select class="od-input" style="height:32px;width:auto;font-size:12.5px;font-weight:600" [value]="l.status" (change)="advance(l, $any($event.target).value)">
              @for (s of statuses; track s) { <option [value]="s">{{ meta(s).label }}</option> }
            </select>
            <a [href]="'mailto:' + l.contactEmail" class="od-btn od-btn--primary od-btn--sm" style="margin-left:auto"><od-icon name="mail" [size]="15" />Отговори</a>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .muted { color: var(--od-500); }
    .filters { display: flex; gap: 8px; margin-bottom: 16px; flex-wrap: wrap; }
    .msg { color: var(--od-600); max-width: 280px; }
    .msg span, .msg { display: block; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .contact-link { display: flex; align-items: center; gap: 9px; font-size: 13.5px; color: var(--od-teal-700); text-decoration: none; margin-bottom: 7px; }
    .contact-row { display: flex; align-items: center; gap: 9px; font-size: 13.5px; color: var(--od-700); }
    .metric { flex: 1; padding: 12px 14px; background: var(--od-50); border-radius: var(--od-r-md); }
    .metric__l { font-size: 11.5px; color: var(--od-500); }
    .metric__v { font-size: 18px; font-weight: 700; color: var(--od-ink); }
    .msg-box { margin: 0; font-size: 14px; color: var(--od-700); line-height: 1.65; padding: 13px 15px; background: var(--od-50); border-radius: var(--od-r-md); border-left: 3px solid var(--od-teal-500); }
  `],
})
export class LeadsPage {
  private api = inject(ApiService);

  statuses = STATUSES;
  filters = FILTERS;
  leads = signal<LeadDto[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);
  filter = signal<string | null>(null);
  openId = signal<string | null>(null);

  opened = computed(() => this.leads().find(l => l.id === this.openId()) ?? null);

  constructor() {
    this.reload();
  }

  meta(status: string): { tone: string; label: string } { return STATUS_META[status] ?? { tone: 'neutral', label: status }; }
  count(status: string): number { return this.leads().filter(l => l.status === status).length; }
  open(l: LeadDto): void { this.openId.set(l.id); }

  setFilter(status: string | null): void {
    this.filter.set(status);
    this.reload();
  }

  advance(lead: LeadDto, status: string): void {
    if (status === lead.status) return;
    this.api.setLeadStatus(lead.id, status).subscribe({
      next: () => this.reload(),
      error: (e) => this.error.set(e?.error?.detail ?? 'Failed to update status'),
    });
  }

  private reload(): void {
    this.loading.set(true);
    this.api.listLeads(this.filter() ?? undefined).subscribe({
      next: (xs) => { this.leads.set(xs); this.loading.set(false); },
      error: (e) => { this.error.set(e?.error?.detail ?? 'Failed to load leads'); this.loading.set(false); },
    });
  }
}
