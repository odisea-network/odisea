import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService, ConnectionHealthDto, ImportJobDto } from './api.service';
import { CANONICAL_FIELDS, FieldMapRow, buildConfigJson, parseConfig } from './connection-config';
import { OdIcon } from './shared/od-icon';

const KINDS = ['JsonApi', 'Xml', 'Manual', 'CsvSftp'];
const KIND_LABEL: Record<string, string> = { JsonApi: 'JSON API', Xml: 'XML', Manual: 'Ръчно', CsvSftp: 'CSV · SFTP' };
const CONN_STATUS: Record<string, { tone: string; label: string }> = {
  Active: { tone: 'success', label: 'Активна' },
  Paused: { tone: 'neutral', label: 'На пауза' },
  Failed: { tone: 'danger', label: 'Неуспешна' },
};
const RUN_STATUS: Record<string, { tone: string; label: string }> = {
  Succeeded: { tone: 'success', label: 'Успешно' },
  Failed: { tone: 'danger', label: 'Неуспешно' },
  Running: { tone: 'info', label: 'В процес' },
};

interface ConnForm {
  name: string;
  kind: string;
  url: string;
  ttl: number;
  fieldMap: FieldMapRow[];
}

@Component({
  selector: 'app-connections-page',
  standalone: true,
  imports: [DatePipe, FormsModule, OdIcon],
  template: `
    <div class="od-page">
      <div class="od-pagehead">
        <div>
          <div class="od-eyebrow">Supply side</div>
          <h1>Връзки с доставчици</h1>
          <p class="od-pagehead__sub">Управлявайте връзките към доставчиците и следете състоянието на импортирането. Единен договор: извличане, разбор, валидиране, нормализиране и обновяване.</p>
        </div>
        <div class="od-pagehead__actions">
          <button type="button" class="od-btn od-btn--secondary" (click)="reload()" [disabled]="loading()"><od-icon name="refresh" [size]="16" />Опресни</button>
          <button type="button" class="od-btn od-btn--primary" (click)="newConnection()"><od-icon name="plus" [size]="16" />Нова връзка</button>
        </div>
      </div>

      <div class="od-stats" style="margin-top:22px;margin-bottom:20px">
        <div class="od-stat"><div class="od-stat__top"><span class="od-stat__label">Активни връзки</span><span class="od-stat__icon"><od-icon name="link" [size]="16" /></span></div><div class="od-stat__value">{{ stats().active }}</div></div>
        <div class="od-stat"><div class="od-stat__top"><span class="od-stat__label">Връзки общо</span><span class="od-stat__icon"><od-icon name="package" [size]="16" /></span></div><div class="od-stat__value">{{ stats().total }}</div></div>
        <div class="od-stat"><div class="od-stat__top"><span class="od-stat__label">Изпълнения · 24ч</span><span class="od-stat__icon gold"><od-icon name="refresh" [size]="16" /></span></div><div class="od-stat__value">{{ stats().runs }}</div></div>
        <div class="od-stat"><div class="od-stat__top"><span class="od-stat__label">Неуспешни · 24ч</span><span class="od-stat__icon"><od-icon name="alert" [size]="16" /></span></div><div class="od-stat__value">{{ stats().failed }}</div></div>
      </div>

      @if (notice()) { <div class="od-notice od-notice--success"><od-icon name="check" [size]="16" /><span>{{ notice() }}</span><button type="button" (click)="notice.set(null)"><od-icon name="x" [size]="15" /></button></div> }
      @if (error()) { <div class="od-notice od-notice--error"><od-icon name="alert" [size]="16" /><span>{{ error() }}</span><button type="button" (click)="error.set(null)"><od-icon name="x" [size]="15" /></button></div> }

      @if (loading()) {
        <p class="muted">Зареждане…</p>
      } @else if (health().length === 0) {
        <div class="od-empty">
          <div class="od-empty__icon"><od-icon name="link" [size]="24" /></div>
          <h3>Все още нямате връзки</h3>
          <p>Добавете първата си връзка към доставчик, за да започнете да импортирате оферти в каталога.</p>
          <button type="button" class="od-btn od-btn--primary" (click)="newConnection()"><od-icon name="plus" [size]="16" />Нова връзка</button>
        </div>
      } @else {
        <div class="od-tablewrap">
          <table class="od-table">
            <thead><tr>
              <th>Връзка</th><th>Последна синхронизация</th><th>Последно изпълнение</th><th>Изпълнения 24ч</th><th style="text-align:right">Действия</th>
            </tr></thead>
            <tbody>
              @for (c of health(); track c.supplierConnectionId) {
                <tr [class.expanded]="expandedId() === c.supplierConnectionId">
                  <td>
                    <div class="conn">
                      <div class="conn__icon"><od-icon [name]="c.kind === 'Manual' ? 'user' : 'link'" [size]="16" /></div>
                      <div>
                        <div class="conn__title">
                          <span class="conn__name">{{ c.name }}</span>
                          <span class="od-badge od-badge--sm od-badge--neutral">{{ kindLabel(c.kind) }}</span>
                          <span class="od-badge od-badge--sm" [class]="'od-badge--' + connStatus(c.status).tone">{{ connStatus(c.status).label }}</span>
                        </div>
                      </div>
                    </div>
                  </td>
                  <td>
                    @if (c.lastSyncedAt) {
                      <span class="sync" [class.stale]="isStale(c.lastSyncedAt)">@if (isStale(c.lastSyncedAt)) { <od-icon name="alert" [size]="14" /> }{{ c.lastSyncedAt | date:'short' }}</span>
                    } @else { <span class="muted">няма</span> }
                  </td>
                  <td>
                    @if (busy().has(c.supplierConnectionId)) {
                      <span class="od-badge od-badge--sm od-badge--info">В процес</span>
                    } @else if (c.lastRunStatus) {
                      <span class="od-badge od-badge--sm" [class]="'od-badge--' + runStatus(c.lastRunStatus).tone">{{ runStatus(c.lastRunStatus).label }}</span>
                    } @else { <span class="muted">—</span> }
                  </td>
                  <td class="od-num"><span style="color:var(--od-700)">{{ c.recentRuns }}</span>@if (c.recentFailures > 0) { <span class="fail">{{ c.recentFailures }} неуспешни</span> }</td>
                  <td>
                    <div class="row-actions">
                      <button type="button" class="od-btn od-btn--subtle od-btn--sm" [disabled]="busy().has(c.supplierConnectionId)" (click)="run(c)"><od-icon [name]="busy().has(c.supplierConnectionId) ? 'clock' : 'play'" [size]="15" />{{ busy().has(c.supplierConnectionId) ? 'Тече…' : 'Стартирай' }}</button>
                      <button type="button" class="od-iconbtn" title="Обнови актуалност" [disabled]="busy().has(c.supplierConnectionId)" (click)="sweep(c)"><od-icon name="refresh" [size]="17" /></button>
                      <button type="button" class="od-iconbtn" [class.od-iconbtn--active]="expandedId() === c.supplierConnectionId" title="История" (click)="toggleJobs(c)"><od-icon name="history" [size]="17" /></button>
                      <button type="button" class="od-iconbtn" title="Редактирай" (click)="editConnection(c)"><od-icon name="settings" [size]="17" /></button>
                      <button type="button" class="od-iconbtn" title="Изтрий" (click)="remove(c)"><od-icon name="trash" [size]="17" /></button>
                    </div>
                  </td>
                </tr>
                @if (expandedId() === c.supplierConnectionId) {
                  <tr class="hist">
                    <td colspan="5">
                      <div class="hist__in">
                        <div class="od-eyebrow" style="margin-bottom:8px">История на изпълненията</div>
                        @if (jobsLoading()) { <span class="muted">Зареждане…</span> }
                        @else {
                          <table class="hist__table">
                            <thead><tr><th>Започнато</th><th>Статус</th><th style="text-align:right">Изтеглени</th><th style="text-align:right">Импортирани</th><th style="text-align:right">Грешки</th></tr></thead>
                            <tbody>
                              @for (j of jobs(); track j.id) {
                                <tr>
                                  <td class="od-mono">{{ j.startedAt | date:'short' }}</td>
                                  <td><span class="od-badge od-badge--sm" [class]="'od-badge--' + runStatus(j.status).tone">{{ runStatus(j.status).label }}</span></td>
                                  <td class="od-num" style="text-align:right">{{ j.offersFetched }}</td>
                                  <td class="od-num" style="text-align:right">{{ j.offersImported }}</td>
                                  <td class="od-num" style="text-align:right" [class.fail]="j.errors.length > 0">{{ j.errors.length }}</td>
                                </tr>
                              } @empty { <tr><td colspan="5" class="muted">Няма записани изпълнения.</td></tr> }
                            </tbody>
                          </table>
                        }
                      </div>
                    </td>
                  </tr>
                }
              }
            </tbody>
          </table>
        </div>
      }
    </div>

    @if (showForm()) {
      <div class="od-modal-overlay" (click)="cancelForm()">
        <div class="od-modal" (click)="$event.stopPropagation()">
          <div class="od-modal__head">
            <div>
              <div class="od-eyebrow" style="margin-bottom:5px">{{ editingId() ? 'Редакция' : 'Нова връзка' }}</div>
              <h2>{{ editingId() ? form.name : 'Добавяне на връзка' }}</h2>
            </div>
            <button type="button" class="od-iconbtn" (click)="cancelForm()"><od-icon name="x" [size]="18" /></button>
          </div>
          <div class="od-modal__body">
            <div class="fields">
              <div><label class="od-label">Име</label><input class="od-input" name="cname" [(ngModel)]="form.name" placeholder="напр. Solvex Travel" /></div>
              <div>
                <label class="od-label">Тип @if (editingId()) { <span class="muted">· заключен при редакция</span> }</label>
                <div class="kinds" [class.locked]="editingId() !== null">
                  @for (k of kinds; track k) {
                    <button type="button" class="od-chip" [class.od-chip--on]="form.kind === k" (click)="form.kind = k">{{ kindLabel(k) }}</button>
                  }
                </div>
              </div>
              @if (form.kind !== 'Manual') {
                <div><label class="od-label">Адрес на емисията (Feed URL)</label>
                  <div class="od-input od-input--withicon"><od-icon name="link" [size]="16" /><input name="curl" [(ngModel)]="form.url" placeholder="https://…" /></div>
                </div>
              }
              <div><label class="od-label">Freshness TTL (часове)</label><input class="od-input" style="max-width:180px" name="cttl" type="number" min="1" [(ngModel)]="form.ttl" /></div>

              <div class="map">
                <div class="map__head">
                  <div>
                    <div class="map__title">Съпоставяне на полета</div>
                    <div class="map__hint">Канонично поле на Odisea → поле от източника</div>
                  </div>
                  <button type="button" class="od-btn od-btn--subtle od-btn--sm" (click)="addRow()"><od-icon name="plus" [size]="15" />Добави ред</button>
                </div>
                @for (row of form.fieldMap; track $index) {
                  <div class="map__row">
                    <select class="od-input" style="height:38px" [(ngModel)]="row.canonical" [name]="'fmc' + $index">
                      @for (f of canonicalFields; track f) { <option [value]="f">{{ f }}</option> }
                    </select>
                    <od-icon name="arrowR" [size]="16" />
                    <input class="od-input" style="height:38px" [(ngModel)]="row.supplier" [name]="'fms' + $index" placeholder="поле от източника" />
                    <button type="button" class="od-iconbtn" title="Премахни" (click)="removeRow($index)"><od-icon name="trash" [size]="16" /></button>
                  </div>
                }
              </div>
            </div>
          </div>
          <div class="od-modal__foot">
            <button type="button" class="od-btn od-btn--secondary" (click)="cancelForm()">Отказ</button>
            <button type="button" class="od-btn od-btn--primary" [disabled]="saving() || !form.name.trim()" (click)="save()"><od-icon name="check" [size]="16" />{{ editingId() ? 'Запази' : 'Създай връзка' }}</button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .muted { color: var(--od-500); }
    .conn { display: flex; align-items: center; gap: 11px; }
    .conn__icon { width: 34px; height: 34px; border-radius: 9px; background: var(--od-teal-50); color: var(--od-teal-600); display: grid; place-items: center; flex: none; }
    .conn__title { display: flex; align-items: center; gap: 7px; flex-wrap: wrap; }
    .conn__name { font-weight: 600; color: var(--od-ink); }
    .sync { display: inline-flex; align-items: center; gap: 5px; color: var(--od-600); }
    .sync.stale { color: var(--od-danger); font-weight: 600; }
    .fail { color: var(--od-danger); margin-left: 6px; font-size: 12px; }
    .row-actions { display: flex; gap: 4px; justify-content: flex-end; }
    .od-table tbody tr.expanded { background: var(--od-50); }
    .hist td { background: var(--od-50); padding: 0; }
    .hist__in { padding: 12px 18px 16px 56px; }
    .hist__table { width: 100%; border-collapse: collapse; font-size: 12.5px; }
    .hist__table th { padding: 5px 8px; font-weight: 600; color: var(--od-500); text-align: left; }
    .hist__table td { padding: 7px 8px; border-top: 1px solid var(--od-border-2); }
    .fields { display: flex; flex-direction: column; gap: 16px; }
    .kinds { display: flex; gap: 6px; flex-wrap: wrap; }
    .kinds.locked { opacity: 0.6; pointer-events: none; }
    .map { border-top: 1px solid var(--od-border-2); padding-top: 16px; }
    .map__head { display: flex; align-items: center; justify-content: space-between; margin-bottom: 10px; }
    .map__title { font-size: 13.5px; font-weight: 700; color: var(--od-ink); }
    .map__hint { font-size: 11.5px; color: var(--od-500); margin-top: 2px; }
    .map__row { display: grid; grid-template-columns: 1fr 20px 1fr 34px; gap: 8px; align-items: center; margin-bottom: 8px; }
  `],
})
export class ConnectionsPage {
  private api = inject(ApiService);

  // A connection's offers are considered stale on the dashboard once its last sync
  // is older than this — mirrors the default freshness TTL, purely a display hint.
  private static readonly StaleAfterMs = 24 * 60 * 60 * 1000;

  health = signal<ConnectionHealthDto[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);
  notice = signal<string | null>(null);
  busy = signal<Set<string>>(new Set());

  expandedId = signal<string | null>(null);
  jobs = signal<ImportJobDto[]>([]);
  jobsLoading = signal(false);

  kinds = KINDS;
  canonicalFields = CANONICAL_FIELDS;
  showForm = signal(false);
  editingId = signal<string | null>(null);
  saving = signal(false);
  form: ConnForm = ConnectionsPage.emptyForm();

  stats = computed(() => {
    const h = this.health();
    return {
      active: h.filter(c => c.status === 'Active').length,
      total: h.length,
      runs: h.reduce((a, c) => a + c.recentRuns, 0),
      failed: h.reduce((a, c) => a + c.recentFailures, 0),
    };
  });

  constructor() {
    this.reload();
  }

  private static emptyForm(): ConnForm {
    return { name: '', kind: 'JsonApi', url: '', ttl: 24, fieldMap: [] };
  }

  kindLabel(k: string): string { return KIND_LABEL[k] ?? k; }
  connStatus(s: string): { tone: string; label: string } { return CONN_STATUS[s] ?? { tone: 'neutral', label: s }; }
  runStatus(s: string): { tone: string; label: string } { return RUN_STATUS[s] ?? { tone: 'neutral', label: s }; }

  newConnection(): void {
    this.editingId.set(null);
    this.form = ConnectionsPage.emptyForm();
    this.error.set(null);
    this.showForm.set(true);
  }

  editConnection(c: ConnectionHealthDto): void {
    this.error.set(null);
    this.api.getConnection(c.supplierConnectionId).subscribe({
      next: (dto) => {
        const { url, fieldMap } = parseConfig(dto.configJson);
        this.form = { name: dto.name, kind: dto.kind, url, ttl: dto.freshnessTtlHours, fieldMap };
        this.editingId.set(dto.id);
        this.showForm.set(true);
      },
      error: (e) => this.error.set(e?.error?.detail ?? 'Failed to load connection'),
    });
  }

  cancelForm(): void {
    this.showForm.set(false);
    this.editingId.set(null);
    this.form = ConnectionsPage.emptyForm();
  }

  addRow(): void {
    this.form.fieldMap = [...this.form.fieldMap, { canonical: 'externalId', supplier: '' }];
  }

  removeRow(index: number): void {
    this.form.fieldMap = this.form.fieldMap.filter((_, i) => i !== index);
  }

  save(): void {
    if (!this.form.name.trim()) return;
    this.saving.set(true);
    this.error.set(null);

    const configJson = buildConfigJson(this.form.url, this.form.fieldMap);
    const id = this.editingId();
    const op = id
      ? this.api.updateConnection(id, { name: this.form.name.trim(), configJson, freshnessTtlHours: this.form.ttl })
      : this.api.createConnection({ kind: this.form.kind, name: this.form.name.trim(), configJson, freshnessTtlHours: this.form.ttl });

    op.subscribe({
      next: (dto) => {
        this.saving.set(false);
        this.notice.set(`${dto.name}: ${id ? 'updated' : 'created'}.`);
        this.cancelForm();
        this.reload();
      },
      error: (e) => { this.saving.set(false); this.error.set(e?.error?.detail ?? 'Save failed'); },
    });
  }

  remove(c: ConnectionHealthDto): void {
    if (!confirm(`Delete connection "${c.name}"? Imported offers remain; only the connection is removed.`)) return;
    this.api.deleteConnection(c.supplierConnectionId).subscribe({
      next: () => { this.notice.set(`${c.name}: deleted.`); this.reload(); },
      error: (e) => this.error.set(e?.error?.detail ?? 'Delete failed'),
    });
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.supplierHealth().subscribe({
      next: (xs) => { this.health.set(xs); this.loading.set(false); },
      error: (e) => { this.error.set(e?.error?.detail ?? 'Failed to load connections'); this.loading.set(false); },
    });
  }

  isStale(lastSyncedAt: string): boolean {
    return Date.now() - new Date(lastSyncedAt).getTime() > ConnectionsPage.StaleAfterMs;
  }

  run(c: ConnectionHealthDto): void {
    this.setBusy(c.supplierConnectionId, true);
    this.notice.set(null);
    this.error.set(null);
    this.api.runConnection(c.supplierConnectionId).subscribe({
      next: (job) => {
        this.setBusy(c.supplierConnectionId, false);
        this.notice.set(`${c.name}: ${job.status} — ${job.offersImported} imported, ${job.offersFetched} fetched.`);
        this.reload();
        if (this.expandedId() === c.supplierConnectionId) this.loadJobs(c.supplierConnectionId);
      },
      error: (e) => { this.setBusy(c.supplierConnectionId, false); this.error.set(e?.error?.detail ?? 'Run failed'); },
    });
  }

  sweep(c: ConnectionHealthDto): void {
    this.setBusy(c.supplierConnectionId, true);
    this.notice.set(null);
    this.error.set(null);
    this.api.sweepConnection(c.supplierConnectionId).subscribe({
      next: (r) => {
        this.setBusy(c.supplierConnectionId, false);
        this.notice.set(`${c.name}: swept — ${r.offersMarkedStale} offer(s) marked stale.`);
        this.reload();
      },
      error: (e) => { this.setBusy(c.supplierConnectionId, false); this.error.set(e?.error?.detail ?? 'Sweep failed'); },
    });
  }

  toggleJobs(c: ConnectionHealthDto): void {
    if (this.expandedId() === c.supplierConnectionId) {
      this.expandedId.set(null);
      return;
    }
    this.expandedId.set(c.supplierConnectionId);
    this.loadJobs(c.supplierConnectionId);
  }

  private loadJobs(id: string): void {
    this.jobsLoading.set(true);
    this.jobs.set([]);
    this.api.connectionJobs(id).subscribe({
      next: (xs) => { this.jobs.set(xs); this.jobsLoading.set(false); },
      error: (e) => { this.error.set(e?.error?.detail ?? 'Failed to load runs'); this.jobsLoading.set(false); },
    });
  }

  private setBusy(id: string, on: boolean): void {
    const next = new Set(this.busy());
    if (on) next.add(id); else next.delete(id);
    this.busy.set(next);
  }
}
