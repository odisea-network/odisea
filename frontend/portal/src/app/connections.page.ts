import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ApiService, ConnectionHealthDto, ImportJobDto } from './api.service';

@Component({
  selector: 'app-connections-page',
  standalone: true,
  imports: [DatePipe],
  template: `
    <div class="head">
      <h2>Supplier connections</h2>
      <button type="button" class="ghost" (click)="reload()" [disabled]="loading()">Refresh</button>
    </div>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (notice()) { <p class="notice">{{ notice() }}</p> }

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

  constructor() {
    this.reload();
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
