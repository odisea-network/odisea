import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, computed, inject, resource, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AgencyDto, OperatorAttentionItem, OperatorDashboardDto } from '../../core/api.types';
import { formatMoney } from '../../core/format';
import { OdDatePipe } from '../../shared/pipes/date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import {
  ButtonDirective, Card, Deadline, EmptyState, Icon, IconName, Kpi, PageHeader, Progress, SegmentOption,
  Segmented, Skeleton,
} from '../../shared/ui';

interface KindMeta {
  icon: IconName;
  tone: 'ok' | 'warn' | 'bad' | 'info' | 'neutral';
  action: string;
  primary: boolean;
}

const KIND: Record<string, KindMeta> = {
  overdue: { icon: 'alert-triangle', tone: 'bad', action: 'Expire', primary: false },
  failed: { icon: 'alert-circle', tone: 'bad', action: 'Review', primary: false },
  ready: { icon: 'check-circle', tone: 'ok', action: 'Confirm', primary: true },
  soon: { icon: 'clock', tone: 'warn', action: 'Record payment', primary: false },
  partial: { icon: 'card', tone: 'info', action: 'Record payment', primary: false },
};

export function kindMeta(kind: string): KindMeta {
  return KIND[kind] ?? { icon: 'info', tone: 'neutral', action: 'Open', primary: false };
}

/** "Bookings 94 · +12 vs the previous 7 days". */
export function deltaLabel(current: number, previous: number, days: number): string {
  const diff = current - previous;
  const period = days === 1 ? 'yesterday' : `the previous ${days} days`;
  if (diff === 0) return `same as ${period}`;
  return `${diff > 0 ? '+' : '−'}${Math.abs(diff)} vs ${period}`;
}

/** The operator's landing page: what needs a decision now, and how the period is going. */
@Component({
  selector: 'app-admin-dashboard',
  imports: [
    RouterLink, MoneyPipe, OdDatePipe,
    ButtonDirective, Card, Deadline, EmptyState, Icon, Kpi, PageHeader, Progress, Segmented, Skeleton,
  ],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class AdminDashboard {
  private readonly http = inject(HttpClient);

  readonly days = signal(7);
  readonly periods: SegmentOption[] = [
    { value: '1', label: 'Today' },
    { value: '7', label: '7 days' },
    { value: '30', label: '30 days' },
  ];

  readonly dashboard = resource({
    params: () => ({ days: this.days() }),
    loader: ({ params }) => firstValueFrom(this.http.get<OperatorDashboardDto>(
      '/api/v1/admin/dashboard', { params: new HttpParams().set('days', params.days) })),
  });
  readonly agencies = resource({
    loader: () => firstValueFrom(this.http.get<AgencyDto[]>('/api/v1/agencies')),
  });

  private readonly agencyNames = computed(() => new Map((this.agencies.value() ?? []).map(a => [a.id, a.name])));

  readonly attention = computed(() => {
    const d = this.dashboard.value();
    return d ? d.readyToConfirm.count + d.dueSoon.count + d.overdue.count + d.failedHolds.count : 0;
  });

  readonly today = computed(() => new Intl.DateTimeFormat('en-GB', { weekday: 'long', day: 'numeric', month: 'long' }).format(new Date()));
  readonly topMax = computed(() => Math.max(1, ...(this.dashboard.value()?.topAgencies.map(a => a.sell) ?? [1])));
  readonly marginShare = computed(() => {
    const d = this.dashboard.value();
    return d && d.confirmedSell > 0 ? Math.round((d.confirmedMargin / d.confirmedSell) * 1000) / 10 : null;
  });

  readonly meta = kindMeta;
  readonly delta = deltaLabel;
  readonly money = formatMoney;

  agencyName(id: string): string {
    return this.agencyNames().get(id) ?? '…';
  }

  setDays(value: string): void {
    this.days.set(Number(value));
  }

  when(item: OperatorAttentionItem): string {
    return item.kind === 'failed' ? 'failed' : item.kind === 'ready' ? 'paid' : 'due';
  }
}
