import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, inject, resource, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { BookingDto, ProblemDetails } from '../../core/api.types';
import { formatDate, formatMoney } from '../../core/format';

const CANCELLABLE = ['Draft', 'PriceConfirmed', 'Confirmed'];

@Component({
  selector: 'app-agency-bookings',
  template: `
    <h2>Bookings</h2>
    @if (error()) {
      <p class="error" role="alert">{{ error() }}</p>
    }
    @if (bookings.isLoading()) {
      <p>Loading…</p>
    } @else {
      <table class="card">
        <thead>
          <tr>
            <th>Ref</th><th>Program / hotel</th><th>Dates</th>
            <th class="num">Price</th><th>Status</th><th></th>
          </tr>
        </thead>
        <tbody>
          @for (b of bookings.value(); track b.id) {
            <tr (click)="toggle(b.id)" class="clickable">
              <td class="strong">{{ b.ref }}</td>
              <td>{{ b.programName }}<br /><span class="muted">{{ b.hotelName }}</span></td>
              <td>{{ fmtDate(b.checkIn) }} – {{ fmtDate(b.checkOut) }}</td>
              <td class="num">{{ fmtMoney(b.sellPrice, b.currency) }}</td>
              <td><span class="chip" [class]="'chip s-' + b.status">{{ b.status }}</span></td>
              <td>
                @if (cancellable(b)) {
                  <button type="button" class="ghost" (click)="cancel(b, $event)" [disabled]="busy()">
                    Cancel
                  </button>
                }
              </td>
            </tr>
            @if (expanded() === b.id) {
              <tr class="detail">
                <td colspan="6">
                  <div class="columns">
                    <div>
                      <h4>Passengers</h4>
                      @for (p of b.passengers; track p.id) {
                        <p>{{ p.isLead ? '★ ' : '' }}{{ p.firstName }} {{ p.lastName }}</p>
                      }
                    </div>
                    <div>
                      <h4>History</h4>
                      @for (h of b.history; track h.at) {
                        <p class="muted">{{ fmtDate(h.at) }} · {{ h.from }} → {{ h.to }}
                          @if (h.note) { <em> — {{ h.note }}</em> }
                        </p>
                      }
                    </div>
                  </div>
                </td>
              </tr>
            }
          } @empty {
            <tr><td colspan="6">No bookings yet — start from Search.</td></tr>
          }
        </tbody>
      </table>
    }
  `,
  styles: `
    .card { background: var(--surface-1); border: 1px solid var(--border); border-radius: .75rem; width: 100%; border-collapse: collapse; }
    th, td { text-align: left; padding: .7rem 1rem; border-bottom: 1px solid var(--border); vertical-align: top; }
    th.num, td.num { text-align: right; }
    .strong { font-weight: 700; }
    .muted { color: var(--text-muted); font-size: .85rem; }
    .clickable { cursor: pointer; }
    .chip { padding: .2rem .55rem; border-radius: 1rem; font-size: .75rem; font-weight: 600; background: var(--surface-2); }
    .chip.s-Confirmed { background: #e2f4e5; color: #1b6e2a; }
    .chip.s-Cancelled, .chip.s-Failed { background: #fbe4e4; color: #a32020; }
    .detail td { background: var(--surface-2); }
    .columns { display: flex; gap: 3rem; }
    h4 { margin: .2rem 0 .4rem; }
    .error { color: var(--danger); }
  `,
})
export class AgencyBookings {
  private readonly http = inject(HttpClient);

  readonly expanded = signal<string | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly bookings = resource({
    loader: () => firstValueFrom(this.http.get<BookingDto[]>('/api/v1/bookings')),
  });

  readonly fmtMoney = formatMoney;
  readonly fmtDate = formatDate;

  cancellable(b: BookingDto): boolean {
    return CANCELLABLE.includes(b.status);
  }

  toggle(id: string): void {
    this.expanded.set(this.expanded() === id ? null : id);
  }

  async cancel(b: BookingDto, event: Event): Promise<void> {
    event.stopPropagation();
    this.busy.set(true);
    this.error.set(null);
    try {
      await firstValueFrom(this.http.post(`/api/v1/bookings/${b.id}/cancel`, {}));
      this.bookings.reload();
    } catch (err) {
      const problem = err instanceof HttpErrorResponse ? (err.error as ProblemDetails) : null;
      this.error.set(problem?.detail ?? 'Cancellation failed.');
    } finally {
      this.busy.set(false);
    }
  }
}
