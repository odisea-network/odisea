import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ProgramListItem } from '../../core/api.types';
import { placeName } from '../../core/program-format';
import { OdDatePipe } from '../../shared/pipes/date.pipe';
import { ButtonDirective, EmptyState, Icon, InputDirective, PageHeader, Pill, Skeleton } from '../../shared/ui';

/** Lowercase without accents, so "turkiye" finds "Türkiye". */
function fold(text: string): string {
  return text.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase();
}

/** Filters by free text (name, place, tagline, tags) and one tag; programs with dates to sell come first. */
export function filterPrograms(programs: ProgramListItem[], query: string, tag: string | null): ProgramListItem[] {
  const words = fold(query).split(/\s+/).filter(w => w.length > 0);
  return programs
    .filter(p => tag === null || p.tags.includes(tag))
    .filter(p => {
      const haystack = fold([p.name, placeName(p.destinationName, p.country), p.country, p.tagline, p.season, ...p.tags].join(' '));
      return words.every(w => haystack.includes(w));
    })
    .sort((a, b) =>
      Number(b.nextDeparture !== null) - Number(a.nextDeparture !== null)
      || (a.nextDeparture ?? '').localeCompare(b.nextDeparture ?? '')
      || a.name.localeCompare(b.name));
}

/** The most used tags first, for the filter chips. */
export function popularTags(programs: ProgramListItem[], limit = 10): string[] {
  const counts = new Map<string, number>();
  for (const p of programs) for (const t of p.tags) counts.set(t, (counts.get(t) ?? 0) + 1);
  return [...counts.entries()].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0])).slice(0, limit).map(([t]) => t);
}

/** Everything the operator currently sells, as cards. */
@Component({
  selector: 'app-agency-programs',
  imports: [FormsModule, RouterLink, OdDatePipe, ButtonDirective, EmptyState, Icon, InputDirective, PageHeader, Pill, Skeleton],
  templateUrl: './programs.html',
  styleUrl: './programs.scss',
})
export class AgencyPrograms {
  private readonly http = inject(HttpClient);

  readonly programs = resource({
    loader: () => firstValueFrom(this.http.get<ProgramListItem[]>('/api/v1/catalog/programs')),
  });

  readonly place = placeName;
  readonly query = signal('');
  readonly tag = signal<string | null>(null);

  readonly tags = computed(() => popularTags(this.programs.value() ?? []));
  readonly filtered = computed(() => filterPrograms(this.programs.value() ?? [], this.query(), this.tag()));

  toggleTag(tag: string): void {
    this.tag.set(this.tag() === tag ? null : tag);
  }

  clear(): void {
    this.query.set('');
    this.tag.set(null);
  }
}
