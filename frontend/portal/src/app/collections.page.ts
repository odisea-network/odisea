import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiService, CollectionDto } from './api.service';
import { forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

interface Row { col: CollectionDto; offerCount: number; }

@Component({
  selector: 'app-collections-page',
  standalone: true,
  imports: [CommonModule],
  template: `
    <h2>Collections</h2>
    <p class="muted" *ngIf="loading()">Loading…</p>
    <p class="error" *ngIf="error()">{{ error() }}</p>
    <table *ngIf="!loading() && !error()">
      <thead>
        <tr>
          <th>Name</th>
          <th>Slug</th>
          <th>Status</th>
          <th>Resolved offers</th>
        </tr>
      </thead>
      <tbody>
        <tr *ngFor="let r of rows()">
          <td>{{ r.col.name }}</td>
          <td><code>{{ r.col.slug }}</code></td>
          <td>{{ r.col.status }}</td>
          <td>{{ r.offerCount }}</td>
        </tr>
      </tbody>
    </table>
  `,
  styles: [`
    table { width: 100%; border-collapse: collapse; }
    th, td { padding: 8px 10px; border-bottom: 1px solid #eee; text-align: left; }
    th { font-weight: 600; background: #fafafa; }
    code { background: #f0f0f0; padding: 2px 6px; border-radius: 3px; }
    .muted { color: #888; }
    .error { color: #c00; }
  `],
})
export class CollectionsPage {
  private api = inject(ApiService);
  rows = signal<Row[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);

  constructor() {
    this.api.listCollections().subscribe({
      next: (cols) => {
        if (cols.length === 0) { this.rows.set([]); this.loading.set(false); return; }
        forkJoin(
          cols.map(c =>
            this.api.resolveCollection(c.slug).pipe(
              map(offers => ({ col: c, offerCount: offers.length } as Row)),
              catchError(() => of({ col: c, offerCount: 0 } as Row)),
            )
          )
        ).subscribe(rs => { this.rows.set(rs); this.loading.set(false); });
      },
      error: (e) => { this.error.set(e.message ?? 'Failed to load'); this.loading.set(false); },
    });
  }
}
