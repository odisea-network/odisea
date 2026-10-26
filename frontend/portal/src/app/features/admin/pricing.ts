import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import {
  AgencyDto, ExchangeRateDto, PricingRuleDto, ProgramListItem, RuleKind, RuleScope, RuleValueType,
  SaveExchangeRateRequest, SavePricingRuleRequest,
} from '../../core/api.types';
import { formatDate, formatMoney } from '../../core/format';
import { problemMessage } from '../../core/problem';
import { OdDatePipe } from '../../shared/pipes/date.pipe';
import {
  Banner, ButtonDirective, Card, DialogService, EmptyState, Field, Icon, InputDirective, PageHeader, Pill,
  SegmentOption, Segmented, Sheet, Skeleton, Switch, ToastService,
} from '../../shared/ui';

type KindView = 'all' | RuleKind;

const KIND_LABELS: Record<string, string> = { Markup: 'Markup', Fee: 'Fee', Commission: 'Commission' };

/** "+10%", "−5%", "+€20 / booking", "10% of sell" — how a rule reads in the list. */
export function ruleValueLabel(rule: Pick<PricingRuleDto, 'kind' | 'valueType' | 'value'>): string {
  const { kind, valueType, value } = rule;
  const sign = value < 0 ? '−' : '+';
  const abs = Math.abs(value);
  if (kind === 'Commission') {
    return valueType === 'Percent' ? `${abs}% of sell` : `${formatMoney(abs)} per booking`;
  }
  return valueType === 'Percent' ? `${sign}${abs}%` : `${sign}${formatMoney(abs)}`;
}

export function ruleValidityLabel(rule: Pick<PricingRuleDto, 'validFrom' | 'validTo'>, fmt: (d: string) => string): string {
  if (rule.validFrom && rule.validTo) return `${fmt(rule.validFrom)} – ${fmt(rule.validTo)}`;
  if (rule.validFrom) return `From ${fmt(rule.validFrom)}`;
  if (rule.validTo) return `Until ${fmt(rule.validTo)}`;
  return 'Always';
}

function emptyRule(): SavePricingRuleRequest {
  return {
    name: '', scope: 'Global', scopeId: null, kind: 'Markup', valueType: 'Percent', value: 10, priority: 100,
    validFrom: null, validTo: null, enabled: true,
  };
}

/** Pricing rules run in priority order inside each kind; the sell price agencies see comes out of them. */
@Component({
  selector: 'app-admin-pricing',
  imports: [
    FormsModule, OdDatePipe,
    Banner, ButtonDirective, Card, EmptyState, Field, Icon, InputDirective, PageHeader, Pill, Segmented, Sheet,
    Skeleton, Switch,
  ],
  templateUrl: './pricing.html',
  styleUrl: './pricing.scss',
})
export class AdminPricing {
  private readonly http = inject(HttpClient);
  private readonly dialogs = inject(DialogService);
  private readonly toasts = inject(ToastService);

  readonly rules = resource({
    loader: () => firstValueFrom(this.http.get<PricingRuleDto[]>('/api/v1/pricing-rules')),
  });
  readonly rates = resource({
    loader: () => firstValueFrom(this.http.get<ExchangeRateDto[]>('/api/v1/exchange-rates')),
  });
  readonly programs = resource({
    loader: () => firstValueFrom(this.http.get<ProgramListItem[]>('/api/v1/programs')),
  });
  readonly agencies = resource({
    loader: () => firstValueFrom(this.http.get<AgencyDto[]>('/api/v1/agencies')),
  });

  readonly kindView = signal<KindView>('all');
  readonly showInactive = signal(true);
  readonly sheetOpen = signal(false);
  readonly editing = signal<PricingRuleDto | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly rateError = signal<string | null>(null);

  readonly kinds = computed<SegmentOption[]>(() => {
    const list = this.rules.value() ?? [];
    const count = (k: string) => list.filter(r => r.kind === k).length;
    return [
      { value: 'all', label: 'All', count: list.length },
      { value: 'Markup', label: 'Markups', count: count('Markup') },
      { value: 'Fee', label: 'Fees', count: count('Fee') },
      { value: 'Commission', label: 'Commissions', count: count('Commission') },
    ];
  });

  readonly filtered = computed(() => {
    const kind = this.kindView();
    return (this.rules.value() ?? [])
      .filter(r => (kind === 'all' || r.kind === kind) && (this.showInactive() || r.enabled))
      .sort((a, b) => a.priority - b.priority || a.name.localeCompare(b.name));
  });

  private readonly programNames = computed(() => new Map((this.programs.value() ?? []).map(p => [p.id, p.name])));
  private readonly agencyNames = computed(() => new Map((this.agencies.value() ?? []).map(a => [a.id, a.name])));

  form: SavePricingRuleRequest = emptyRule();
  rateForm: SaveExchangeRateRequest = { fromCurrency: 'BGN', rate: 1.95583, asOf: new Date().toISOString().slice(0, 10) };

  readonly valueLabel = ruleValueLabel;
  readonly fmt = formatDate;

  kindLabel(kind: string): string {
    return KIND_LABELS[kind] ?? kind;
  }

  scopeName(rule: PricingRuleDto): string {
    if (rule.scope === 'Program') return this.programNames().get(rule.scopeId ?? '') ?? 'Unknown program';
    if (rule.scope === 'Agency') return this.agencyNames().get(rule.scopeId ?? '') ?? 'Unknown agency';
    return 'All programs and agencies';
  }

  validity(rule: PricingRuleDto, fmt: (d: string) => string): string {
    return ruleValidityLabel(rule, fmt);
  }

  setKind(value: string): void {
    this.kindView.set(value as KindView);
  }

  openCreate(): void {
    this.editing.set(null);
    this.form = emptyRule();
    this.error.set(null);
    this.sheetOpen.set(true);
  }

  openEdit(rule: PricingRuleDto): void {
    this.editing.set(rule);
    this.form = {
      name: rule.name, scope: rule.scope as RuleScope, scopeId: rule.scopeId, kind: rule.kind as RuleKind,
      valueType: rule.valueType as RuleValueType, value: rule.value, priority: rule.priority,
      validFrom: rule.validFrom, validTo: rule.validTo, enabled: rule.enabled,
    };
    this.error.set(null);
    this.sheetOpen.set(true);
  }

  onScopeChange(scope: RuleScope): void {
    this.form.scope = scope;
    this.form.scopeId = scope === 'Global' ? null
      : scope === 'Program' ? (this.programs.value()?.[0]?.id ?? null)
      : (this.agencies.value()?.[0]?.id ?? null);
  }

  formValid(): boolean {
    const f = this.form;
    return f.name.trim().length > 0 && (f.scope === 'Global' || !!f.scopeId)
      && Number.isFinite(f.value) && f.value >= -1000 && f.value <= 1000
      && Number.isInteger(f.priority) && f.priority >= 0 && f.priority <= 1000
      && (!f.validFrom || !f.validTo || f.validFrom <= f.validTo);
  }

  async save(): Promise<void> {
    if (!this.formValid()) return;
    const body: SavePricingRuleRequest = {
      ...this.form, name: this.form.name.trim(), value: Number(this.form.value), priority: Number(this.form.priority),
      scopeId: this.form.scope === 'Global' ? null : this.form.scopeId,
      validFrom: this.form.validFrom || null, validTo: this.form.validTo || null,
    };
    const editing = this.editing();
    await this.act(() => editing
      ? firstValueFrom(this.http.put<PricingRuleDto>(`/api/v1/pricing-rules/${editing.id}`, body))
      : firstValueFrom(this.http.post<PricingRuleDto>('/api/v1/pricing-rules', body)),
      editing ? 'Rule saved' : 'Rule created', () => this.sheetOpen.set(false));
  }

  async toggle(rule: PricingRuleDto, enabled: boolean): Promise<void> {
    const body: SavePricingRuleRequest = {
      name: rule.name, scope: rule.scope as RuleScope, scopeId: rule.scopeId, kind: rule.kind as RuleKind,
      valueType: rule.valueType as RuleValueType, value: rule.value, priority: rule.priority,
      validFrom: rule.validFrom, validTo: rule.validTo, enabled,
    };
    await this.act(() => firstValueFrom(this.http.put<PricingRuleDto>(`/api/v1/pricing-rules/${rule.id}`, body)),
      enabled ? `${rule.name} is active` : `${rule.name} is inactive`);
  }

  async remove(rule: PricingRuleDto): Promise<void> {
    const decision = await this.dialogs.confirm({
      title: `Delete “${rule.name}”?`,
      message: 'New prices stop applying it immediately. Prices already quoted keep their own breakdown.',
      confirmLabel: 'Delete rule',
      destructive: true,
    });
    if (!decision) return;
    await this.act(() => firstValueFrom(this.http.delete(`/api/v1/pricing-rules/${rule.id}`)), 'Rule deleted',
      () => this.sheetOpen.set(false));
  }

  rateValid(): boolean {
    return /^[A-Za-z]{3}$/.test(this.rateForm.fromCurrency.trim()) && this.rateForm.rate > 0 && !!this.rateForm.asOf;
  }

  async addRate(): Promise<void> {
    if (!this.rateValid()) return;
    this.busy.set(true);
    this.rateError.set(null);
    try {
      await firstValueFrom(this.http.post<ExchangeRateDto>('/api/v1/exchange-rates', {
        fromCurrency: this.rateForm.fromCurrency.trim().toUpperCase(), rate: Number(this.rateForm.rate), asOf: this.rateForm.asOf,
      }));
      this.toasts.show(`Rate for ${this.rateForm.fromCurrency.toUpperCase()} saved`);
      this.rates.reload();
    } catch (err) {
      this.rateError.set(problemMessage(err, 'Could not save the rate.'));
    } finally {
      this.busy.set(false);
    }
  }

  private async act(request: () => Promise<unknown>, done: string, after?: () => void): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await request();
      this.rules.reload();
      this.toasts.show(done);
      after?.();
    } catch (err) {
      this.error.set(problemMessage(err, 'The change was not saved.'));
    } finally {
      this.busy.set(false);
    }
  }
}
