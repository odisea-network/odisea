import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, computed, inject, resource, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AgencyDto, OperatorAttentionItem, OperatorDashboardDto } from '../../core/api.types';
import { formatMoney } from '../../core/format';
import { t } from '../../core/i18n/i18n';
import { OdDatePipe } from '../../shared/pipes/date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { TPipe } from '../../shared/pipes/t.pipe';
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
  overdue: { icon: 'alert-triangle', tone: 'bad', action: t('adminDashboard.actionExpire'), primary: false },
  failed: { icon: 'alert-circle', tone: 'bad', action: t('adminDashboard.actionReview'), primary: false },
  ready: { icon: 'check-circle', tone: 'ok', action: t('adminDashboard.actionConfirm'), primary: true },
  soon: { icon: 'clock', tone: 'warn', action: t('adminDashboard.actionRecordPayment'), primary: false },
  partial: { icon: 'card', tone: 'info', action: t('adminDashboard.actionRecordPayment'), primary: false },
};

export function kindMeta(kind: string): KindMeta {
  return KIND[kind] ?? { icon: 'info', tone: 'neutral', action: t('adminDashboard.actionOpen'), primary: false };
}

/** "Резервации 94 · +12 спрямо предходните 7 дни". */
export function deltaLabel(current: number, previous: number, days: number): string {
  const diff = current - previous;
  if (diff === 0) return days === 1 ? t('adminDashboard.sameAsYesterday') : t('adminDashboard.sameAsPrevious', { days });
  const change = `${diff > 0 ? '+' : '−'}${Math.abs(diff)}`;
  return days === 1 ? t('adminDashboard.vsYesterday', { change }) : t('adminDashboard.vsPrevious', { change, days });
}

/** The operator's landing page: what needs a decision now, and how the period is going. */
@Component({
  selector: 'app-admin-dashboard',
  imports: [
    RouterLink, MoneyPipe, OdDatePipe, TPipe,
    ButtonDirective, Card, Deadline, EmptyState, Icon, Kpi, PageHeader, Progress, Segmented, Skeleton,
  ],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class AdminDashboard {
  private readonly http = inject(HttpClient);

  readonly days = signal(7);
  readonly periods: SegmentOption[] = [
    { value: '1', label: t('adminDashboard.periodToday') },
    { value: '7', label: t('adminDashboard.periodDays', { days: 7 }) },
    { value: '30', label: t('adminDashboard.periodDays', { days: 30 }) },
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

  readonly today = computed(() => new Intl.DateTimeFormat('bg-BG', { weekday: 'long', day: 'numeric', month: 'long' }).format(new Date()));
  readonly headerSub = computed(() => {
    if (!this.dashboard.value()) return this.today();
    const n = this.attention();
    return `${this.today()} · ${t(n === 1 ? 'adminDashboard.attentionOne' : 'adminDashboard.attentionMany', { n })}`;
  });
  readonly periodTitle = computed(() =>
    this.days() === 1 ? t('adminDashboard.periodToday') : t('adminDashboard.lastDays', { days: this.days() }));
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
    return t(item.kind === 'failed' ? 'adminDashboard.whenFailed' : item.kind === 'ready' ? 'adminDashboard.whenPaid' : 'adminDashboard.whenDue');
  }
}
