import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, computed, effect, inject, input, resource, signal, untracked, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import {
  CreateDepartureRequest, DepartureDto, HotelDto, HotelMappingDto, ProgramDetail, UpdateProgramRequest,
} from '../../core/api.types';
import { problemMessage } from '../../core/problem';
import { placeName } from '../../core/program-format';
import { OdDatePipe } from '../../shared/pipes/date.pipe';
import {
  Banner, ButtonDirective, Card, DialogService, EmptyState, Field, Icon, InputDirective, PageHeader, Pill, Sheet,
  Skeleton, TabOption, Tabs, ToastService,
} from '../../shared/ui';
import { MonthGrid, monthGrid, nightsBetween, shiftMonth } from './calendar';
import {
  CONTENT_LIMITS, ContentDraft, ProgramTab, draftFrom, draftProblems, expectedDays, toRequest,
} from './program-content.model';
import { ProgramContentForm } from './program-content-form';
import { ProgramGallery } from './program-gallery';
import { ProgramItinerary } from './program-itinerary';
import { AdminProgramReadiness } from './program-readiness';
import { programTone } from './programs';

/** One program: what agencies read, its photos, the departures and hotels they can book, and whether it can go live. */
@Component({
  selector: 'app-admin-program-detail',
  imports: [
    FormsModule, RouterLink, OdDatePipe,
    Banner, ButtonDirective, Card, EmptyState, Field, Icon, InputDirective, PageHeader, Pill, Sheet, Skeleton, Tabs,
    AdminProgramReadiness, ProgramContentForm, ProgramGallery, ProgramItinerary,
  ],
  templateUrl: './program-detail.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './program-detail.scss',
})
export class AdminProgramDetail {
  private readonly http = inject(HttpClient);
  private readonly dialogs = inject(DialogService);
  private readonly toasts = inject(ToastService);

  readonly id = input.required<string>();

  readonly tab = signal<ProgramTab>('content');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly editOpen = signal(false);
  readonly departureOpen = signal(false);
  readonly hotelOpen = signal(false);
  readonly calendar = signal<{ year: number; month: number } | null>(null);

  readonly tone = programTone;
  readonly place = placeName;
  readonly today = new Date().toISOString().slice(0, 10);
  readonly descriptionMax = CONTENT_LIMITS.description;

  readonly loaded = resource({
    params: () => ({ id: this.id() }),
    loader: ({ params }) => firstValueFrom(this.http.get<ProgramDetail>(`/api/v1/programs/${params.id}`)),
  });
  readonly program = computed(() => this.loaded.value() ?? null);

  /** Every hotel of the destination; attached ones are filtered from it. */
  readonly hotels = resource({
    params: () => {
      const destinationId = this.program()?.destinationId;
      return destinationId ? { destinationId } : undefined;
    },
    loader: ({ params }) => firstValueFrom(this.http.get<HotelDto[]>(
      '/api/v1/hotels', { params: new HttpParams().set('destinationId', params.destinationId) })),
  });
  readonly availableHotels = computed(() => {
    const ids = new Set(this.program()?.hotelIds ?? []);
    return (this.hotels.value() ?? []).filter(h => !ids.has(h.id));
  });

  /** Which attached hotels have a code at the program's supplier. */
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

  /** The content and itinerary being edited; saved together with one PUT. */
  readonly draft = signal<ContentDraft | null>(null);
  private baseline = '';

  readonly readiness = computed(() => this.program()?.readiness ?? null);
  readonly blocking = computed(() =>
    (this.readiness()?.checks ?? []).filter(c => c.required && !c.passed).map(c => c.title.toLowerCase()));
  readonly expected = computed(() => {
    const p = this.program();
    return p ? expectedDays(p) : null;
  });

  readonly tabs = computed<TabOption[]>(() => {
    const p = this.program();
    return [
      { value: 'content', label: 'Content' },
      { value: 'itinerary', label: 'Itinerary', count: this.draft()?.itinerary.length ?? null },
      { value: 'gallery', label: 'Gallery', count: p?.images.length ?? null },
      { value: 'departures', label: 'Departures', count: p?.departures.length ?? null },
      { value: 'hotels', label: 'Hotels', count: p?.hotelIds.length ?? null },
    ];
  });

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

  constructor() {
    // A reload (after any save) refreshes the draft unless the operator has unsaved edits.
    effect(() => {
      const p = this.program();
      if (!p) return;
      untracked(() => {
        if (this.draft() === null || !this.dirty()) this.resetDraft(p);
      });
    });
  }

  /** Called from the template on every change detection; the draft is a plain object edited by ngModel. */
  dirty(): boolean {
    const d = this.draft();
    const p = this.program();
    return !!d && !!p && JSON.stringify(toRequest(d, p.slug)) !== this.baseline;
  }

  problems(): string[] {
    const d = this.draft();
    return d ? draftProblems(d) : [];
  }

  canPublish(): boolean {
    const p = this.program();
    return !!p && p.status === 'Draft' && !!p.readiness?.canPublish && !this.dirty();
  }

  publishBlockedBy(): string | null {
    if (this.dirty()) return 'Save your changes first';
    const missing = this.blocking();
    return missing.length > 0 ? `Missing: ${missing.join(', ')}` : null;
  }

  isMapped(hotelId: string): boolean | null {
    const mapped = this.mappings.value();
    return mapped ? mapped.has(hotelId) : null;
  }

  nights(): number {
    return nightsBetween(this.departure.startDate, this.departure.endDate);
  }

  setTab(value: string): void {
    this.tab.set(value as ProgramTab);
  }

  go(target: ProgramTab | 'details'): void {
    if (target === 'details') this.openEdit();
    else this.tab.set(target);
  }

  shift(by: number): void {
    const g = this.grid();
    if (g) this.calendar.set(shiftMonth(g.year, g.month, by));
  }

  discard(): void {
    const p = this.program();
    if (p) this.resetDraft(p);
    this.error.set(null);
  }

  async saveContent(): Promise<void> {
    const p = this.program();
    const d = this.draft();
    if (!p || !d) return;
    const problems = draftProblems(d);
    if (problems.length > 0) {
      this.error.set(problems.join(' '));
      return;
    }
    const request = toRequest(d, p.slug);
    await this.act(async () => {
      const saved = await firstValueFrom(this.http.put<ProgramDetail>(`/api/v1/programs/${p.id}/content`, request));
      // The server normalises (trims, lowercases tags); start again from what it stored.
      this.resetDraft(saved);
    }, 'Content saved');
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
    const open = (p.readiness?.checks ?? []).filter(c => !c.required && !c.passed);
    const decision = await this.dialogs.confirm({
      title: `Publish ${p.name}?`,
      message: open.length > 0
        ? `Every active agency can search it from now on. ${open.length} recommended ${open.length === 1 ? 'item is' : 'items are'} still open: ${open.map(c => c.title.toLowerCase()).join(', ')}.`
        : 'Every active agency can search it from now on. Existing bookings are not affected.',
      facts: [{ label: 'Readiness score', value: `${p.readiness?.score ?? 0} / 100` }],
      confirmLabel: 'Publish',
    });
    if (!decision) return;
    await this.act(() => firstValueFrom(this.http.post<ProgramDetail>(`/api/v1/programs/${p.id}/publish`, {})),
      `${p.name} is live`);
  }

  reload(): void {
    this.loaded.reload();
  }

  private resetDraft(p: ProgramDetail): void {
    const draft = draftFrom(p);
    this.baseline = JSON.stringify(toRequest(draft, p.slug));
    this.draft.set(draft);
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
