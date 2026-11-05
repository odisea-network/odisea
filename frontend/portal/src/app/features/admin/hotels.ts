import { HttpClient } from '@angular/common/http';
import { Component, computed, effect, inject, input, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { DestinationDto, HotelDto, HotelMappingDto } from '../../core/api.types';
import { problemMessage } from '../../core/problem';
import {
  Banner, ButtonDirective, Card, EmptyState, Field, Icon, InputDirective, PageHeader, Pill, Sheet, Skeleton,
  ToastService,
} from '../../shared/ui';

/** Hotels programs can allow; supplier codes map each onto the provider's hotel catalogue. */
@Component({
  selector: 'app-admin-hotels',
  imports: [
    FormsModule,
    Banner, ButtonDirective, Card, EmptyState, Field, Icon, InputDirective, PageHeader, Pill, Sheet, Skeleton,
  ],
  templateUrl: './hotels.html',
  styleUrl: './hotels.scss',
})
export class AdminHotels {
  private readonly http = inject(HttpClient);
  private readonly toasts = inject(ToastService);

  /** Query parameter: open the mappings of this hotel straight away (linked from a program). */
  readonly hotelIdParam = input<string | undefined>(undefined, { alias: 'hotelId' });

  readonly destinations = resource({
    loader: () => firstValueFrom(this.http.get<DestinationDto[]>('/api/v1/destinations')),
  });
  readonly hotels = resource({
    loader: () => firstValueFrom(this.http.get<HotelDto[]>('/api/v1/hotels')),
  });

  readonly destinationFilter = signal('');
  readonly query = signal('');
  readonly createOpen = signal(false);
  readonly mappingFor = signal<HotelDto | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly destinationNames = computed(() =>
    new Map((this.destinations.value() ?? []).map(d => [d.id, `${d.name}, ${d.country}`])));

  readonly filtered = computed(() => {
    const dest = this.destinationFilter();
    const q = this.query().trim().toLowerCase();
    return (this.hotels.value() ?? []).filter(h =>
      (!dest || h.destinationId === dest) && (!q || h.name.toLowerCase().includes(q)));
  });

  readonly mappings = resource({
    params: () => {
      const h = this.mappingFor();
      return h ? { id: h.id } : undefined;
    },
    loader: ({ params }) => firstValueFrom(this.http.get<HotelMappingDto[]>(`/api/v1/hotels/${params.id}/mappings`)),
  });

  form = { name: '', destinationId: '', stars: 4 };
  mapping = { providerCode: 'mock', externalHotelCode: '' };

  constructor() {
    effect(() => {
      const id = this.hotelIdParam();
      const hotel = id ? this.hotels.value()?.find(h => h.id === id) : undefined;
      if (hotel && !this.mappingFor()) this.openMappings(hotel);
    });
  }

  destinationName(id: string): string {
    return this.destinationNames().get(id) ?? '—';
  }

  stars(count: number): number[] {
    return Array.from({ length: count });
  }

  openCreate(): void {
    this.form = { name: '', destinationId: this.destinationFilter() || this.destinations.value()?.[0]?.id || '', stars: 4 };
    this.error.set(null);
    this.createOpen.set(true);
  }

  formValid(): boolean {
    return this.form.name.trim().length > 0 && this.form.destinationId !== '' && this.form.stars >= 1 && this.form.stars <= 5;
  }

  async create(): Promise<void> {
    if (!this.formValid()) return;
    await this.act(() => firstValueFrom(this.http.post<HotelDto>('/api/v1/hotels', {
      name: this.form.name.trim(), destinationId: this.form.destinationId, stars: Number(this.form.stars),
    })), `${this.form.name.trim()} added`, () => { this.createOpen.set(false); this.hotels.reload(); });
  }

  openMappings(h: HotelDto): void {
    this.mapping = { providerCode: 'mock', externalHotelCode: '' };
    this.error.set(null);
    this.mappingFor.set(h);
  }

  mappingValid(): boolean {
    return this.mapping.providerCode.trim().length > 0 && this.mapping.externalHotelCode.trim().length > 0;
  }

  async addMapping(): Promise<void> {
    const h = this.mappingFor();
    if (!h || !this.mappingValid()) return;
    await this.act(() => firstValueFrom(this.http.post<HotelMappingDto>(`/api/v1/hotels/${h.id}/mappings`, {
      providerCode: this.mapping.providerCode.trim().toLowerCase(), externalHotelCode: this.mapping.externalHotelCode.trim(),
    })), 'Mapping added', () => { this.mapping = { ...this.mapping, externalHotelCode: '' }; this.mappings.reload(); });
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
