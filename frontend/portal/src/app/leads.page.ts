import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ApiService, LeadDto } from './api.service';

const STATUSES = ['New', 'Contacted', 'Converted', 'Closed'];

@Component({
  selector: 'app-leads-page',
  standalone: true,
  imports: [DatePipe],
  template: `
    <h2>Leads</h2>
    @if (error()) { <p class="error">{{ error() }}</p> }

    <div class="filters">
      <button type="button" [class.active]="filter() === null" (click)="setFilter(null)">All</button>
      @for (s of statuses; track s) {
        <button type="button" [class.active]="filter() === s" (click)="setFilter(s)">{{ s }}</button>
      }
    </div>

    @if (loading()) {
      <p class="muted">Loading…</p>
    } @else {
      <table>
        <thead>
          <tr><th>Received</th><th>Contact</th><th>Kind</th><th>Message</th><th>Status</th><th></th></tr>
        </thead>
        <tbody>
          @for (l of leads(); track l.id) {
            <tr>
              <td>{{ l.createdAt | date:'short' }}</td>
              <td>
                <div class="name">{{ l.contactName }}</div>
                <div class="sub">{{ l.contactEmail }}@if (l.contactPhone) { · {{ l.contactPhone }} }</div>
              </td>
              <td>
                <span class="kind" [class.booking]="l.kind === 'BookingRequest'">{{ l.kind }}</span>
                @if (l.kind === 'BookingRequest') {
                  <div class="sub">{{ l.partySize }} pax · {{ l.nights }} nights</div>
                }
              </td>
              <td class="msg">{{ l.message || '—' }}</td>
              <td><span class="status" [attr.data-s]="l.status">{{ l.status }}</span></td>
              <td>
                <select [value]="l.status" (change)="advance(l, $any($event.target).value)">
                  @for (s of statuses; track s) { <option [value]="s">{{ s }}</option> }
                </select>
              </td>
            </tr>
          } @empty {
            <tr><td colspan="6" class="muted">No leads{{ filter() ? ' with status ' + filter() : '' }} yet.</td></tr>
          }
        </tbody>
      </table>
    }
  `,
  styles: [`
    .filters { display: flex; gap: 8px; margin-bottom: 16px; }
    .filters button {
      padding: 6px 14px; border: 1px solid #ccc; border-radius: 999px;
      background: #fff; color: #555; cursor: pointer; font-size: 0.85rem;
    }
    .filters button.active { background: #0a2540; color: #fff; border-color: #0a2540; }
    table { width: 100%; border-collapse: collapse; }
    th, td { padding: 10px; border-bottom: 1px solid #eee; text-align: left; vertical-align: top; }
    th { font-weight: 600; background: #fafafa; }
    .name { font-weight: 500; }
    .sub { color: #888; font-size: 0.8rem; }
    .msg { max-width: 280px; color: #444; font-size: 0.9rem; }
    .kind { font-size: 0.8rem; color: #667; }
    .kind.booking { color: #186; font-weight: 600; }
    .status { font-size: 0.8rem; padding: 2px 8px; border-radius: 999px; background: #eee; }
    .status[data-s="New"] { background: #e7f0ff; color: #1a5; }
    .status[data-s="Converted"] { background: #e6f7ee; color: #186; }
    .status[data-s="Closed"] { background: #f1f1f1; color: #888; }
    select { padding: 5px 8px; border: 1px solid #ccc; border-radius: 6px; }
    .muted { color: #888; }
    .error { color: #c00; }
  `],
})
export class LeadsPage {
  private api = inject(ApiService);

  statuses = STATUSES;
  leads = signal<LeadDto[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);
  filter = signal<string | null>(null);

  constructor() {
    this.reload();
  }

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
