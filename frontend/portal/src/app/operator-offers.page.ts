import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { ApiService, CreateOfferRequest, OfferDto } from './api.service';

const BOARD_OPTIONS = ['RoomOnly', 'BedAndBreakfast', 'HalfBoard', 'FullBoard', 'AllInclusive'];
const TRANSPORT_OPTIONS = ['Plane', 'Bus', 'Own'];

function emptyForm(): CreateOfferRequest {
  return {
    title: '', description: '', country: '', city: '',
    price: 0, currency: 'EUR', boardBasis: 'HalfBoard', transport: 'Plane',
    durationNights: 7, tags: [], imageUrl: null,
  };
}

@Component({
  selector: 'app-operator-offers-page',
  standalone: true,
  imports: [FormsModule, DecimalPipe],
  template: `
    <h2>My offers</h2>
    @if (error()) { <p class="error">{{ error() }}</p> }

    <section class="form-card">
      <h3>{{ editingId() ? 'Edit offer' : 'New offer' }}</h3>
      <div class="grid">
        <label>Title<input name="title" [(ngModel)]="form.title" /></label>
        <label>Country<input name="country" [(ngModel)]="form.country" placeholder="GR" /></label>
        <label>City<input name="city" [(ngModel)]="form.city" /></label>
        <label>Price<input name="price" type="number" [(ngModel)]="form.price" /></label>
        <label>Currency<input name="currency" [(ngModel)]="form.currency" /></label>
        <label>Nights<input name="nights" type="number" [(ngModel)]="form.durationNights" /></label>
        <label>Board
          <select name="board" [(ngModel)]="form.boardBasis">
            @for (b of boards; track b) { <option [value]="b">{{ b }}</option> }
          </select>
        </label>
        <label>Transport
          <select name="transport" [(ngModel)]="form.transport">
            @for (t of transports; track t) { <option [value]="t">{{ t }}</option> }
          </select>
        </label>
        <label>Image URL<input name="image" [(ngModel)]="form.imageUrl" /></label>
      </div>
      <label class="full">Description<textarea name="description" rows="2" [(ngModel)]="form.description"></textarea></label>
      <div class="actions">
        <button type="button" (click)="save()" [disabled]="saving() || !form.title || !form.country">
          {{ saving() ? 'Saving…' : editingId() ? 'Save changes' : 'Create draft' }}
        </button>
        @if (editingId()) { <button type="button" class="ghost" (click)="cancelEdit()">Cancel</button> }
      </div>
    </section>

    @if (loading()) {
      <p class="muted">Loading…</p>
    } @else {
      <table>
        <thead>
          <tr><th>Title</th><th>Country</th><th>Board</th><th>Price</th><th>Status</th><th></th></tr>
        </thead>
        <tbody>
          @for (o of offers(); track o.id) {
            <tr>
              <td>{{ o.title }}</td>
              <td>{{ o.country }}</td>
              <td>{{ o.boardBasis }}</td>
              <td>{{ o.price | number:'1.0-2' }} {{ o.currency }}</td>
              <td><span class="status" [class.published]="o.status === 'Published'">{{ o.status }}</span></td>
              <td class="row-actions">
                <button type="button" (click)="edit(o)">Edit</button>
                @if (o.status === 'Published') {
                  <button type="button" (click)="unpublish(o)">Unpublish</button>
                } @else {
                  <button type="button" (click)="publish(o)">Publish</button>
                }
              </td>
            </tr>
          } @empty {
            <tr><td colspan="6" class="muted">No offers yet — create one above.</td></tr>
          }
        </tbody>
      </table>
    }
  `,
  styles: [`
    .form-card { border: 1px solid #e3e3e3; border-radius: 8px; padding: 16px; margin-bottom: 24px; background: #fafbfc; }
    .form-card h3 { margin: 0 0 12px; font-size: 1rem; }
    .grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; }
    label { display: flex; flex-direction: column; gap: 4px; font-size: 0.85rem; color: #555; }
    label.full { margin-top: 12px; }
    input, select, textarea { padding: 7px 9px; border: 1px solid #ccc; border-radius: 6px; font: inherit; }
    .actions { margin-top: 14px; display: flex; gap: 10px; }
    button { padding: 8px 14px; border: 0; border-radius: 6px; background: #0a2540; color: #fff; cursor: pointer; }
    button.ghost { background: transparent; color: #555; border: 1px solid #ccc; }
    button:disabled { opacity: 0.5; cursor: default; }
    table { width: 100%; border-collapse: collapse; }
    th, td { padding: 8px 10px; border-bottom: 1px solid #eee; text-align: left; }
    th { font-weight: 600; background: #fafafa; }
    .row-actions { display: flex; gap: 8px; }
    .row-actions button { padding: 4px 10px; font-size: 0.85rem; background: #335; }
    .status { font-size: 0.8rem; color: #a36; }
    .status.published { color: #186; }
    .muted { color: #888; }
    .error { color: #c00; }
  `],
})
export class OperatorOffersPage {
  private api = inject(ApiService);

  boards = BOARD_OPTIONS;
  transports = TRANSPORT_OPTIONS;

  offers = signal<OfferDto[]>([]);
  loading = signal(true);
  saving = signal(false);
  error = signal<string | null>(null);
  editingId = signal<string | null>(null);
  form: CreateOfferRequest = emptyForm();

  constructor() {
    this.reload();
  }

  private reload(): void {
    this.loading.set(true);
    this.api.listMyOffers().subscribe({
      next: (xs) => { this.offers.set(xs); this.loading.set(false); },
      error: (e) => { this.error.set(e?.error?.detail ?? 'Failed to load offers'); this.loading.set(false); },
    });
  }

  save(): void {
    this.saving.set(true);
    this.error.set(null);
    const id = this.editingId();
    const op = id ? this.api.updateOffer(id, this.form) : this.api.createOffer(this.form);
    op.subscribe({
      next: () => { this.saving.set(false); this.cancelEdit(); this.reload(); },
      error: (e) => { this.error.set(e?.error?.detail ?? 'Save failed'); this.saving.set(false); },
    });
  }

  edit(o: OfferDto): void {
    this.editingId.set(o.id);
    this.form = {
      title: o.title, description: o.description, country: o.country, city: o.city,
      price: o.price, currency: o.currency, boardBasis: o.boardBasis, transport: o.transport,
      durationNights: o.durationNights, tags: o.tags ?? [], imageUrl: o.imageUrl ?? null,
    };
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.form = emptyForm();
  }

  publish(o: OfferDto): void {
    this.api.publishOffer(o.id).subscribe({ next: () => this.reload() });
  }

  unpublish(o: OfferDto): void {
    this.api.unpublishOffer(o.id).subscribe({ next: () => this.reload() });
  }
}
