import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { DestinationDto, HotelDto, LocationMappingDto } from '../../core/api.types';
import { problemMessage } from '../../core/problem';
import {
  Banner, ButtonDirective, Card, EmptyState, Field, Icon, InputDirective, PageHeader, Pill, Sheet, Skeleton,
  ToastService,
} from '../../shared/ui';

/** Destinations group hotels and programs; supplier codes map them onto the provider's locations. */
@Component({
  selector: 'app-admin-destinations',
  imports: [
    FormsModule,
    Banner, ButtonDirective, Card, EmptyState, Field, Icon, InputDirective, PageHeader, Pill, Sheet, Skeleton,
  ],
  templateUrl: './destinations.html',
  styleUrl: './destinations.scss',
})
export class AdminDestinations {
  private readonly http = inject(HttpClient);
  private readonly toasts = inject(ToastService);

  readonly destinations = resource({
    loader: () => firstValueFrom(this.http.get<DestinationDto[]>('/api/v1/destinations')),
  });
  readonly hotels = resource({
    loader: () => firstValueFrom(this.http.get<HotelDto[]>('/api/v1/hotels')),
  });

  readonly createOpen = signal(false);
  readonly mappingFor = signal<DestinationDto | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly hotelCounts = computed(() => {
    const counts = new Map<string, number>();
    for (const h of this.hotels.value() ?? []) counts.set(h.destinationId, (counts.get(h.destinationId) ?? 0) + 1);
    return counts;
  });

  readonly mappings = resource({
    params: () => {
      const d = this.mappingFor();
      return d ? { id: d.id } : undefined;
    },
    loader: ({ params }) => firstValueFrom(this.http.get<LocationMappingDto[]>(`/api/v1/destinations/${params.id}/mappings`)),
  });

  form = { name: '', country: 'BG' };
  mapping = { providerCode: 'mock', externalLocationCode: '' };

  hotelCount(id: string): number {
    return this.hotelCounts().get(id) ?? 0;
  }

  openCreate(): void {
    this.form = { name: '', country: 'BG' };
    this.error.set(null);
    this.createOpen.set(true);
  }

  formValid(): boolean {
    return this.form.name.trim().length > 0 && /^[A-Za-z]{2}$/.test(this.form.country.trim());
  }

  async create(): Promise<void> {
    if (!this.formValid()) return;
    await this.act(() => firstValueFrom(this.http.post<DestinationDto>('/api/v1/destinations', {
      name: this.form.name.trim(), country: this.form.country.trim().toUpperCase(),
    })), `${this.form.name.trim()} added`, () => { this.createOpen.set(false); this.destinations.reload(); });
  }

  openMappings(d: DestinationDto): void {
    this.mapping = { providerCode: 'mock', externalLocationCode: '' };
    this.error.set(null);
    this.mappingFor.set(d);
  }

  mappingValid(): boolean {
    return this.mapping.providerCode.trim().length > 0 && this.mapping.externalLocationCode.trim().length > 0;
  }

  async addMapping(): Promise<void> {
    const d = this.mappingFor();
    if (!d || !this.mappingValid()) return;
    await this.act(() => firstValueFrom(this.http.post<LocationMappingDto>(`/api/v1/destinations/${d.id}/mappings`, {
      providerCode: this.mapping.providerCode.trim().toLowerCase(), externalLocationCode: this.mapping.externalLocationCode.trim(),
    })), 'Mapping added', () => { this.mapping = { ...this.mapping, externalLocationCode: '' }; this.mappings.reload(); });
  }

  closeMappings(open: boolean): void {
    if (!open) this.mappingFor.set(null);
  }

  private async act(request: () => Promise<unknown>, done: string, after?: () => void): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await request();
      this.toasts.show(done);
      after?.();
    } catch (err) {
      this.error.set(problemMessage(err, 'The change was not saved.'));
    } finally {
      this.busy.set(false);
    }
  }
}
