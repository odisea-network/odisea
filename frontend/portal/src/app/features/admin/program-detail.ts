import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, computed, inject, input, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import {
  CreateDepartureRequest, DepartureDto, DestinationDto, HotelDto, HotelMappingDto, ProgramDetail,
  UpdateProgramRequest,
} from '../../core/api.types';
import { problemMessage } from '../../core/problem';
import { OdDatePipe } from '../../shared/pipes/date.pipe';
import {
  Banner, ButtonDirective, Card, DialogService, EmptyState, Field, Icon, IconName, InputDirective, PageHeader,
  Pill, Progress, Sheet, Skeleton, TabOption, Tabs, ToastService,
} from '../../shared/ui';
import { MonthGrid, monthGrid, nightsBetween, shiftMonth } from './calendar';
import { programTone } from './programs';

type Tab = 'departures' | 'hotels' | 'general';

export interface PublishCheck {
  title: string;
  sub: string;
  ok: boolean;
}

/** Mirrors what the API will insist on at publish, plus the soft checks operations care about. */
export function publishChecks(
  program: Pick<ProgramDetail, 'name' | 'destinationId' | 'departures' | 'hotelIds'>,
  mappedHotels: number | null,
  departuresOnOrAfter: string,
): PublishCheck[] {
  const future = program.departures.filter(d => d.startDate >= departuresOnOrAfter);
  const noTransport = program.departures.filter(d => !d.transportNote?.trim()).length;
  return [
    { title: 'Name and destination', sub: program.name, ok: program.name.trim().length > 0 && !!program.destinationId },
    {
      title: 'At least one upcoming departure',
      sub: future.length > 0 ? `${future.length} upcoming of ${program.departures.length}` : 'None yet',
      ok: future.length > 0,
    },
    { title: 'At least one allowed hotel', sub: `${program.hotelIds.length} allowed`, ok: program.hotelIds.length > 0 },
    {
      title: 'Transport on every departure',
      sub: noTransport === 0 ? 'All departures have a transport note' : `${noTransport} without a transport note`,
      ok: noTransport === 0,
    },
    {
      title: 'Hotels mapped to the supplier',
      sub: mappedHotels === null ? 'Checking…' : `${mappedHotels} of ${program.hotelIds.length} mapped`,
      ok: mappedHotels !== null && mappedHotels === program.hotelIds.length && program.hotelIds.length > 0,
    },
  ];
}

/** One program: its departures, the hotels agencies can pick, and whether it is ready to go live. */
@Component({
  selector: 'app-admin-program-detail',
  imports: [
    FormsModule, RouterLink, OdDatePipe,
    Banner, ButtonDirective, Card, EmptyState, Field, Icon, InputDirective, PageHeader, Pill, Progress, Sheet,
    Skeleton, Tabs,
  ],
  templateUrl: './program-detail.html',
  styleUrl: './program-detail.scss',
})
export class AdminProgramDetail {
  private readonly http = inject(HttpClient);
  private readonly dialogs = inject(DialogService);
  private readonly toasts = inject(ToastService);

  readonly id = input.required<string>();

  readonly tab = signal<Tab>('departures');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly editOpen = signal(false);
  readonly departureOpen = signal(false);
  readonly hotelOpen = signal(false);
  readonly calendar = signal<{ year: number; month: number } | null>(null);

  readonly tone = programTone;
  readonly today = new Date().toISOString().slice(0, 10);

  readonly loaded = resource({
    params: () => ({ id: this.id() }),
    loader: ({ params }) => firstValueFrom(this.http.get<ProgramDetail>(`/api/v1/programs/${params.id}`)),
  });
  readonly program = computed(() => this.loaded.value() ?? null);

  readonly destinations = resource({
    loader: () => firstValueFrom(this.http.get<DestinationDto[]>('/api/v1/destinations')),
  });
  readonly destination = computed(() => {
    const p = this.program();
    return p ? this.destinations.value()?.find(d => d.id === p.destinationId) ?? null : null;
  });

  /** Every hotel of the destination; attached ones are filtered from it. */
  readonly hotels = resource({
    params: () => {
      const destinationId = this.program()?.destinationId;
      return destinationId ? { destinationId } : undefined;
    },
    loader: ({ params }) => firstValueFrom(this.http.get<HotelDto[]>(
      '/api/v1/hotels', { params: new HttpParams().set('destinationId', params.destinationId) })),
  });
  readonly attachedHotels = computed(() => {
    const ids = new Set(this.program()?.hotelIds ?? []);
    return (this.hotels.value() ?? []).filter(h => ids.has(h.id));
  });
  readonly availableHotels = computed(() => {
    const ids = new Set(this.program()?.hotelIds ?? []);
    return (this.hotels.value() ?? []).filter(h => !ids.has(h.id));
  });

  /** Supplier mappings of the attached hotels, for the publish checklist. */
  readonly mappings = resource({
    params: () => {
      const p = this.program();
      return p && p.hotelIds.length > 0 ? { ids: p.hotelIds, provider: p.providerCode } : undefined;
    },
    loader: async ({ params }) => {
      const lists = await Promise.all(params.ids.map(id =>
        firstValueFrom(this.http.get<HotelMappingDto[]>(`/api/v1/hotels/${id}/mappings`))));
      const mapped = new Set<string>();
      lists.forEach((list, i) => {
        if (list.some(m => m.providerCode === params.provider)) mapped.add(params.ids[i]);
      });
      return mapped;
    },
  });
  readonly mappedCount = computed(() => {
    const p = this.program();
    if (!p) return null;
    if (p.hotelIds.length === 0) return 0;
    const mapped = this.mappings.value();
    return mapped ? mapped.size : null;
  });

  readonly checks = computed(() => {
    const p = this.program();
    return p ? publishChecks(p, this.mappedCount(), this.today) : [];
  });
  readonly checksPassed = computed(() => this.checks().filter(c => c.ok).length);
  /** Only the API's own rules block the button; the rest are advice. */
  readonly canPublish = computed(() => {
    const p = this.program();
    return !!p && p.status === 'Draft' && p.departures.length > 0 && p.hotelIds.length > 0;
  });

  readonly tabs = computed<TabOption[]>(() => [
    { value: 'departures', label: 'Departures', count: this.program()?.departures.length ?? null },
    { value: 'hotels', label: 'Allowed hotels', count: this.program()?.hotelIds.length ?? null },
    { value: 'general', label: 'General' },
  ]);

  readonly grid = computed<MonthGrid | null>(() => {
    const p = this.program();
    if (!p) return null;
    const marks = new Map<string, 'ok' | 'warn'>();
    for (const d of p.departures) marks.set(d.startDate, d.transportNote?.trim() ? 'ok' : 'warn');
    const focus = this.calendar() ?? this.defaultMonth(p.departures);
    return monthGrid(focus.year, focus.month, marks);
  });

  readonly dow = ['Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa', 'Su'];

  edit: UpdateProgramRequest = { name: '', season: '', description: '' };
  departure: CreateDepartureRequest = { startDate: '', endDate: '', transportNote: '' };
  hotelToAttach = '';

  isMapped(hotelId: string): boolean | null {
    const mapped = this.mappings.value();
    return mapped ? mapped.has(hotelId) : null;
  }

  checkIcon(ok: boolean): IconName {
    return ok ? 'check' : 'alert-circle';
  }

  nights(): number {
    return nightsBetween(this.departure.startDate, this.departure.endDate);
  }

  setTab(value: string): void {
    this.tab.set(value as Tab);
  }

  shift(by: number): void {
    const g = this.grid();
    if (g) this.calendar.set(shiftMonth(g.year, g.month, by));
  }

  openEdit(): void {
    const p = this.program();
    if (!p) return;
    this.edit = { name: p.name, season: p.season ?? '', description: p.description ?? '' };
    this.error.set(null);
    this.editOpen.set(true);
  }

  async saveEdit(): Promise<void> {
    const p = this.program();
    if (!p || !this.edit.name.trim()) return;
    await this.act(() => firstValueFrom(this.http.put<ProgramDetail>(`/api/v1/programs/${p.id}`, {
      name: this.edit.name.trim(), season: this.edit.season?.trim() || null, description: this.edit.description?.trim() || null,
    })), 'Program saved', () => this.editOpen.set(false));
  }

  openDeparture(): void {
    this.departure = { startDate: '', endDate: '', transportNote: '' };
    this.error.set(null);
    this.departureOpen.set(true);
  }

  departureValid(): boolean {
    return !!this.departure.startDate && !!this.departure.endDate && this.nights() > 0;
  }

  async addDeparture(): Promise<void> {
    const p = this.program();
    if (!p || !this.departureValid()) return;
    await this.act(() => firstValueFrom(this.http.post<DepartureDto>(`/api/v1/programs/${p.id}/departures`, {
      startDate: this.departure.startDate, endDate: this.departure.endDate,
      transportNote: this.departure.transportNote?.trim() || null,
    })), 'Departure added', () => this.departureOpen.set(false));
  }

  openHotel(): void {
    this.hotelToAttach = this.availableHotels()[0]?.id ?? '';
    this.error.set(null);
    this.hotelOpen.set(true);
  }

  async attachHotel(): Promise<void> {
    const p = this.program();
    if (!p || !this.hotelToAttach) return;
    await this.act(() => firstValueFrom(this.http.post(`/api/v1/programs/${p.id}/hotels/${this.hotelToAttach}`, {})),
      'Hotel allowed in this program', () => this.hotelOpen.set(false));
  }

  async publish(): Promise<void> {
    const p = this.program();
    if (!p || !this.canPublish()) return;
    const failing = this.checks().filter(c => !c.ok);
    const decision = await this.dialogs.confirm({
      title: `Publish ${p.name}?`,
      message: failing.length > 0
        ? `Every active agency can search it from now on. ${failing.length} advisory ${failing.length === 1 ? 'check is' : 'checks are'} still open: ${failing.map(c => c.title.toLowerCase()).join(', ')}.`
        : 'Every active agency can search it from now on. Existing bookings are not affected.',
      confirmLabel: 'Publish',
    });
    if (!decision) return;
    await this.act(() => firstValueFrom(this.http.post<ProgramDetail>(`/api/v1/programs/${p.id}/publish`, {})),
      `${p.name} is live`);
  }

  private defaultMonth(departures: DepartureDto[]): { year: number; month: number } {
    const next = departures.map(d => d.startDate).filter(d => d >= this.today).sort()[0]
      ?? departures.map(d => d.startDate).sort().at(-1)
      ?? this.today;
    const [y, m] = next.split('-').map(Number);
    return { year: y, month: m - 1 };
  }

  private async act(request: () => Promise<unknown>, done: string, after?: () => void): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await request();
      this.loaded.reload();
      this.toasts.show(done);
      after?.();
    } catch (err) {
      this.error.set(problemMessage(err, 'The change was not saved.'));
    } finally {
      this.busy.set(false);
    }
  }
}
