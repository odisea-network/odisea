import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AgencyDetailDto, SaveAgencyRequest } from '../../core/api.types';
import { problemMessage } from '../../core/problem';
import { OdDatePipe } from '../../shared/pipes/date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import {
  Banner, ButtonDirective, Card, EmptyState, Field, Icon, InputDirective, PageHeader, Pill, SegmentOption,
  Segmented, Sheet, Skeleton, ToastService,
} from '../../shared/ui';

type View = 'all' | 'active' | 'suspended';

export function agencyInitials(name: string): string {
  return name.split(/\s+/).filter(Boolean).slice(0, 2).map(w => w[0].toUpperCase()).join('');
}

/** Partner agencies: who can search and book, under which limits. */
@Component({
  selector: 'app-admin-agencies',
  imports: [
    FormsModule, RouterLink, MoneyPipe, OdDatePipe,
    Banner, ButtonDirective, Card, EmptyState, Field, Icon, InputDirective, PageHeader, Pill, Segmented, Sheet,
    Skeleton,
  ],
  templateUrl: './agencies.html',
  styleUrl: './agencies.scss',
})
export class AdminAgencies {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);

  readonly agencies = resource({
    loader: () => firstValueFrom(this.http.get<AgencyDetailDto[]>('/api/v1/agencies')),
  });

  readonly view = signal<View>('all');
  readonly query = signal('');
  readonly createOpen = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly initials = agencyInitials;

  readonly views = computed<SegmentOption[]>(() => {
    const list = this.agencies.value() ?? [];
    return [
      { value: 'all', label: 'All', count: list.length },
      { value: 'active', label: 'Active', count: list.filter(a => a.status === 'Active').length },
      { value: 'suspended', label: 'Suspended', count: list.filter(a => a.status === 'Suspended').length },
    ];
  });

  readonly filtered = computed(() => {
    const view = this.view();
    const q = this.query().trim().toLowerCase();
    return (this.agencies.value() ?? []).filter(a =>
      (view === 'all' || (view === 'active' ? a.status === 'Active' : a.status === 'Suspended'))
      && (!q || a.name.toLowerCase().includes(q) || (a.contactEmail ?? '').toLowerCase().includes(q)
        || (a.billing?.legalName ?? '').toLowerCase().includes(q) || (a.billing?.eik ?? '').includes(q)));
  });

  // Create form.
  form: SaveAgencyRequest = { name: '', country: 'BG', creditLimit: 0, contactEmail: '' };

  readonly canCreate = computed(() => true);

  setView(value: string): void {
    this.view.set(value as View);
  }

  open(a: AgencyDetailDto): void {
    void this.router.navigate(['/admin/agencies', a.id]);
  }

  openCreate(): void {
    this.form = { name: '', country: 'BG', creditLimit: 0, contactEmail: '' };
    this.error.set(null);
    this.createOpen.set(true);
  }

  formValid(): boolean {
    return this.form.name.trim().length > 0 && /^[A-Za-z]{2}$/.test(this.form.country.trim()) && this.form.creditLimit >= 0;
  }

  async create(): Promise<void> {
    if (!this.formValid()) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      const created = await firstValueFrom(this.http.post<AgencyDetailDto>('/api/v1/agencies', {
        name: this.form.name.trim(),
        country: this.form.country.trim().toUpperCase(),
        creditLimit: this.form.creditLimit,
        contactEmail: this.form.contactEmail?.trim() || null,
      }));
      this.createOpen.set(false);
      this.toasts.show(`${created.name} added`);
      await this.router.navigate(['/admin/agencies', created.id]);
    } catch (err) {
      this.error.set(problemMessage(err, 'Could not create the agency.'));
    } finally {
      this.busy.set(false);
    }
  }
}
