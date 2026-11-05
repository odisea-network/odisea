import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { CreateProgramRequest, DestinationDto, ProgramDetail, ProgramDto } from '../../core/api.types';
import { problemMessage } from '../../core/problem';
import { OdDatePipe } from '../../shared/pipes/date.pipe';
import {
  Banner, ButtonDirective, Card, EmptyState, Field, Icon, InputDirective, PageHeader, Pill, SegmentOption,
  Segmented, Sheet, Skeleton, ToastService,
} from '../../shared/ui';

type View = 'all' | 'Draft' | 'Published' | 'Archived';

const PROGRAM_STATUS_TONE: Record<string, 'neutral' | 'ok' | 'warn' | undefined> = {
  Draft: 'warn',
  Published: 'ok',
  Archived: 'neutral',
};

export function programTone(status: string): 'neutral' | 'ok' | 'warn' {
  return PROGRAM_STATUS_TONE[status] ?? 'neutral';
}

/** What the operator sells: programs the agencies can search once published. */
@Component({
  selector: 'app-admin-programs',
  imports: [
    FormsModule, RouterLink, OdDatePipe,
    Banner, ButtonDirective, Card, EmptyState, Field, Icon, InputDirective, PageHeader, Pill, Segmented, Sheet,
    Skeleton,
  ],
  templateUrl: './programs.html',
  styleUrl: './programs.scss',
})
export class AdminPrograms {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);

  readonly programs = resource({
    loader: () => firstValueFrom(this.http.get<ProgramDto[]>('/api/v1/programs')),
  });
  readonly destinations = resource({
    loader: () => firstValueFrom(this.http.get<DestinationDto[]>('/api/v1/destinations')),
  });

  readonly view = signal<View>('all');
  readonly createOpen = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly tone = programTone;

  readonly destinationNames = computed(() =>
    new Map((this.destinations.value() ?? []).map(d => [d.id, `${d.name}, ${d.country}`])));

  readonly views = computed<SegmentOption[]>(() => {
    const list = this.programs.value() ?? [];
    const count = (s: string) => list.filter(p => p.status === s).length;
    return [
      { value: 'all', label: 'All', count: list.length },
      { value: 'Draft', label: 'Drafts', count: count('Draft') },
      { value: 'Published', label: 'Published', count: count('Published') },
      { value: 'Archived', label: 'Archived', count: count('Archived') },
    ];
  });

  readonly filtered = computed(() => {
    const view = this.view();
    return (this.programs.value() ?? []).filter(p => view === 'all' || p.status === view);
  });

  form: CreateProgramRequest = { name: '', destinationId: '', providerCode: 'mock', season: '', description: '' };

  destinationName(id: string): string {
    return this.destinationNames().get(id) ?? '—';
  }

  setView(value: string): void {
    this.view.set(value as View);
  }

  open(p: ProgramDto): void {
    void this.router.navigate(['/admin/programs', p.id]);
  }

  openCreate(): void {
    this.form = { name: '', destinationId: this.destinations.value()?.[0]?.id ?? '', providerCode: 'mock', season: '', description: '' };
    this.error.set(null);
    this.createOpen.set(true);
  }

  formValid(): boolean {
    return this.form.name.trim().length > 0 && this.form.destinationId !== '' && this.form.providerCode.trim().length > 0;
  }

  async create(): Promise<void> {
    if (!this.formValid()) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      const created = await firstValueFrom(this.http.post<ProgramDetail>('/api/v1/programs', {
        name: this.form.name.trim(),
        destinationId: this.form.destinationId,
        providerCode: this.form.providerCode.trim().toLowerCase(),
        season: this.form.season?.trim() || null,
        description: this.form.description?.trim() || null,
      }));
      this.createOpen.set(false);
      this.toasts.show(`${created.name} created as a draft`);
      await this.router.navigate(['/admin/programs', created.id]);
    } catch (err) {
      this.error.set(problemMessage(err, 'Could not create the program.'));
    } finally {
      this.busy.set(false);
    }
  }
}
