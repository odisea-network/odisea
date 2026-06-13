import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService, ConnectionHealthDto, ImportJobDto } from './api.service';
import { CANONICAL_FIELDS, FieldMapRow, buildConfigJson, parseConfig } from './connection-config';

const KINDS = ['JsonApi', 'Xml', 'Manual', 'CsvSftp'];

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
  imports: [DatePipe, FormsModule],
  template: `
    <div class="head">
      <h2>Supplier connections</h2>
      <div class="head-actions">
        <button type="button" (click)="newConnection()">New connection</button>
        <button type="button" class="ghost" (click)="reload()" [disabled]="loading()">Refresh</button>
      </div>
    </div>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (notice()) { <p class="notice">{{ notice() }}</p> }

    @if (showForm()) {
      <section class="form-card">
        <h3>{{ editingId() ? 'Edit connection' : 'New connection' }}</h3>
        <div class="grid">
          <label>Name<input name="cname" [(ngModel)]="form.name" /></label>
          <label>Kind
            <select name="ckind" [(ngModel)]="form.kind" [disabled]="editingId() !== null">
              @for (k of kinds; track k) { <option [value]="k">{{ k }}</option> }
            </select>
          </label>
          <label>Freshness TTL (hours)<input name="cttl" type="number" [(ngModel)]="form.ttl" /></label>
          @if (form.kind !== 'Manual') {
            <label class="full">Feed URL<input name="curl" [(ngModel)]="form.url" placeholder="https://supplier.example/offers.json" /></label>
          }
        </div>

        @if (form.kind !== 'Manual') {
          <div class="fieldmap">
            <div class="fm-head">
              <span>Field mapping <span class="muted">(optional — map canonical fields to the supplier's names)</span></span>
              <button type="button" class="link" (click)="addRow()">+ Add field</button>
            </div>
            @for (row of form.fieldMap; track $index) {
              <div class="fm-row">
                <select [(ngModel)]="row.canonical" [name]="'fmcanon' + $index">
                  @for (f of canonicalFields; track f) { <option [value]="f">{{ f }}</option> }
                </select>
                <span class="arrow">→</span>
                <input [(ngModel)]="row.supplier" [name]="'fmsup' + $index" placeholder="supplier's field name" />
                <button type="button" class="link danger" (click)="removeRow($index)">remove</button>
              </div>
            }
          </div>
        }

        <div class="actions">
          <button type="button" (click)="save()" [disabled]="saving() || !form.name.trim()">
            {{ saving() ? 'Saving…' : editingId() ? 'Save changes' : 'Create' }}
          </button>
          <button type="button" class="ghost" (click)="cancelForm()">Cancel</button>
        </div>
      </section>
    }

    @if (loading()) {
      <p class="muted">Loading…</p>
    } @else {
      <table>
        <thead>
          <tr>
            <th>Connection</th><th>Last sync</th><th>Last run</th>
            <th>Runs (24h)</th><th></th>
          </tr>
        </thead>
        <tbody>
          @for (c of health(); track c.supplierConnectionId) {
            <tr>
              <td>
                <div class="name">{{ c.name }}</div>
                <div class="sub">{{ c.kind }} · <span class="status" [attr.data-s]="c.status">{{ c.status }}</span></div>
              </td>
              <td>
                @if (c.lastSyncedAt) {
                  <span [class.stale]="isStale(c.lastSyncedAt)">{{ c.lastSyncedAt | date:'short' }}</span>
                } @else { <span class="muted">never</span> }
              </td>
              <td>
                @if (c.lastRunStatus) {
                  <span class="run" [attr.data-s]="c.lastRunStatus">{{ c.lastRunStatus }}</span>
                } @else { <span class="muted">—</span> }
              </td>
              <td>
                {{ c.recentRuns }}
                @if (c.recentFailures > 0) { <span class="fail">· {{ c.recentFailures }} failed</span> }
              </td>
              <td class="row-actions">
                <button type="button" (click)="run(c)" [disabled]="busy().has(c.supplierConnectionId)">
                  {{ busy().has(c.supplierConnectionId) ? '…' : 'Run' }}
                </button>
                <button type="button" class="ghost" (click)="sweep(c)" [disabled]="busy().has(c.supplierConnectionId)">Sweep</button>
                <button type="button" class="link" (click)="toggleJobs(c)">
                  {{ expandedId() === c.supplierConnectionId ? 'Hide' : 'History' }}
                </button>
                <button type="button" class="link" (click)="editConnection(c)">Edit</button>
                <button type="button" class="link danger" (click)="remove(c)">Delete</button>
              </td>
            </tr>
            @if (expandedId() === c.supplierConnectionId) {
              <tr class="jobs-row">
                <td colspan="5">
                  @if (jobsLoading()) {
                    <span class="muted">Loading runs…</span>
                  } @else {
                    <table class="jobs">
                      <thead>
                        <tr><th>Started</th><th>Status</th><th>Fetched</th><th>Imported</th><th>Errors</th></tr>
                      </thead>
                      <tbody>
                        @for (j of jobs(); track j.id) {
                          <tr>
                            <td>{{ j.startedAt | date:'short' }}</td>
                            <td><span class="run" [attr.data-s]="j.status">{{ j.status }}</span></td>
                            <td>{{ j.offersFetched }}</td>
                            <td>{{ j.offersImported }}</td>
                            <td class="errs">{{ j.errors.length ? j.errors.join('; ') : '—' }}</td>
                          </tr>
                        } @empty {
                          <tr><td colspan="5" class="muted">No runs recorded yet.</td></tr>
                        }
                      </tbody>
                    </table>
                  }
                </td>
              </tr>
            }
          } @empty {
            <tr><td colspan="5" class="muted">No supplier connections yet.</td></tr>
          }
        </tbody>
      </table>
    }
  `,
  styles: [`
    .head { display: flex; align-items: center; justify-content: space-between; }
    .head-actions { display: flex; gap: 10px; }
    .form-card { border: 1px solid #e3e3e3; border-radius: 8px; padding: 16px; margin-bottom: 20px; background: #fafbfc; }
    .form-card h3 { margin: 0 0 12px; font-size: 1rem; }
    .grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; }
    label { display: flex; flex-direction: column; gap: 4px; font-size: 0.85rem; color: #555; }
    label.full { grid-column: 1 / -1; }
    input, select { padding: 7px 9px; border: 1px solid #ccc; border-radius: 6px; font: inherit; }
    .fieldmap { margin-top: 14px; }
    .fm-head { display: flex; justify-content: space-between; align-items: center; font-size: 0.85rem; margin-bottom: 8px; }
    .fm-row { display: flex; align-items: center; gap: 8px; margin-bottom: 6px; }
    .fm-row select { flex: 0 0 160px; }
    .fm-row input { flex: 1; }
    .arrow { color: #888; }
    .actions { margin-top: 14px; display: flex; gap: 10px; }
    .danger { color: #c0392b; }
    table { width: 100%; border-collapse: collapse; }
    th, td { padding: 10px; border-bottom: 1px solid #eee; text-align: left; vertical-align: top; }
    th { font-weight: 600; background: #fafafa; }
    .name { font-weight: 500; }
    .sub { color: #888; font-size: 0.8rem; }
    .status, .run { font-size: 0.75rem; padding: 2px 8px; border-radius: 999px; background: #eee; }
    .status[data-s="Active"] { background: #e6f7ee; color: #186; }
    .status[data-s="Paused"] { background: #fff4e0; color: #a66; }
    .status[data-s="Failed"] { background: #fdecea; color: #c0392b; }
    .run[data-s="Succeeded"] { background: #e6f7ee; color: #186; }
    .run[data-s="Failed"] { background: #fdecea; color: #c0392b; }
    .run[data-s="Running"] { background: #e7f0ff; color: #1a5; }
    .stale { color: #c0392b; }
    .fail { color: #c0392b; font-size: 0.8rem; }
    .row-actions { display: flex; gap: 8px; white-space: nowrap; }
    button { padding: 5px 12px; border: 0; border-radius: 6px; background: #0a2540; color: #fff; cursor: pointer; font-size: 0.85rem; }
    button.ghost { background: transparent; color: #555; border: 1px solid #ccc; }
    button.link { background: transparent; color: #0a2540; padding: 5px 4px; }
    button:disabled { opacity: 0.5; cursor: default; }
    .jobs-row td { background: #fafbfc; }
    table.jobs { margin: 4px 0; }
    table.jobs th, table.jobs td { padding: 6px 8px; font-size: 0.85rem; border-bottom: 1px solid #eee; }
    .errs { max-width: 320px; color: #c0392b; }
    .muted { color: #888; }
    .error { color: #c00; }
    .notice { color: #186; background: #e6f7ee; padding: 8px 12px; border-radius: 6px; }
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

  constructor() {
    this.reload();
  }

  private static emptyForm(): ConnForm {
    return { name: '', kind: 'JsonApi', url: '', ttl: 24, fieldMap: [] };
  }

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
