import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, input, resource } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ProgramDetail } from '../../core/api.types';
import { t } from '../../core/i18n/i18n';
import { mealLabel, placeName, transportLabel } from '../../core/program-format';
import { OdDatePipe } from '../../shared/pipes/date.pipe';
import { TPipe } from '../../shared/pipes/t.pipe';
import {
  ButtonDirective, Card, EmptyState, Icon, Markdown, PageHeader, Pill, Skeleton,
} from '../../shared/ui';
import { ProgramPhotos } from './program-photos';

/** The full program as agencies sell it: content, itinerary, policies and the dates to book. */
@Component({
  selector: 'app-agency-program',
  imports: [
    RouterLink, OdDatePipe, TPipe, ButtonDirective, Card, EmptyState, Icon, Markdown, PageHeader, Pill, Skeleton, ProgramPhotos,
  ],
  templateUrl: './program.html',
  styleUrl: './program.scss',
})
export class AgencyProgram {
  private readonly http = inject(HttpClient);

  readonly id = input.required<string>();

  readonly loaded = resource({
    params: () => ({ id: this.id() }),
    loader: ({ params }) => firstValueFrom(this.http.get<ProgramDetail>(`/api/v1/catalog/programs/${params.id}`)),
  });
  readonly program = computed(() => this.loaded.value() ?? null);

  readonly today = new Date().toISOString().slice(0, 10);
  readonly upcoming = computed(() => (this.program()?.departures ?? []).filter(d => d.startDate >= this.today));

  readonly meals = mealLabel;
  readonly place = placeName;
  readonly transport = transportLabel;

  hasPolicies(p: ProgramDetail): boolean {
    const c = p.content;
    return !!(c.cancellationPolicy || c.paymentTerms || c.insurancePolicy || c.childPolicy);
  }

  groupSize(p: ProgramDetail): string | null {
    const { minGroupSize: min, maxGroupSize: max } = p.content;
    if (min && max) return t('program.groupRange', { min, max });
    if (min) return t('program.groupMin', { min });
    if (max) return t('program.groupMax', { max });
    return null;
  }

  stars(count: number): string {
    return '★'.repeat(Math.max(0, Math.min(5, count)));
  }
}
