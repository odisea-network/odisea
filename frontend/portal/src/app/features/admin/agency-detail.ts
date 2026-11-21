import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, computed, inject, input, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import {
  AgencyDetailDto, AgencyPlan, AgencyUserDto, ApiKeyCreatedDto, ApiKeyDto, CreateAgencyUserRequest,
  OperatorBookingSummary, PagedResult, UpdateAgencyBillingRequest, UpdateAgencyRequest,
} from '../../core/api.types';
import { AuthService } from '../../core/auth.service';
import { t } from '../../core/i18n/i18n';
import { problemMessage } from '../../core/problem';
import { OdDatePipe, OdDateTimePipe } from '../../shared/pipes/date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { TPipe } from '../../shared/pipes/t.pipe';
import {
  Banner, ButtonDirective, Card, DialogService, EmptyState, Field, Icon, InputDirective, PageHeader, Pill,
  Sheet, Skeleton, TabOption, Tabs, ToastService,
} from '../../shared/ui';
import { agencyInitials } from './agencies';

type Tab = 'overview' | 'users' | 'keys' | 'bookings';
type KeyExpiry = '90d' | '1y' | 'never';

export type ApiKeyState = 'Active' | 'Expired' | 'Revoked';

export function apiKeyState(key: Pick<ApiKeyDto, 'revokedAt' | 'expiresAt'>, now = new Date()): ApiKeyState {
  if (key.revokedAt) return 'Revoked';
  if (key.expiresAt && Date.parse(key.expiresAt) < now.getTime()) return 'Expired';
  return 'Active';
}

export function keyExpiresAt(choice: KeyExpiry, now = new Date()): string | null {
  if (choice === 'never') return null;
  const at = new Date(now);
  if (choice === '90d') at.setUTCDate(at.getUTCDate() + 90);
  else at.setUTCFullYear(at.getUTCFullYear() + 1);
  return at.toISOString();
}

export const PLANS: AgencyPlan[] = ['Core', 'Ledger', 'Workspace'];

export const PLAN_SUMMARY: Record<AgencyPlan, string> = {
  Core: t('agencyDetail.planSummary.Core'),
  Ledger: t('agencyDetail.planSummary.Ledger'),
  Workspace: t('agencyDetail.planSummary.Workspace'),
};

/** What the operator is told before a plan change; a move down the list takes features away. */
export function planChangeMessage(from: AgencyPlan, to: AgencyPlan): string {
  const includes = t('agencyDetail.planIncludes', { plan: to, summary: PLAN_SUMMARY[to].toLowerCase() });
  return PLANS.indexOf(to) < PLANS.indexOf(from)
    ? `${includes} ${t('agencyDetail.planLoses', { plan: to })}`
    : includes;
}

const ROLE_LABELS: Record<string, string> = { AgencyAdmin: t('roles.agencyAdmin'), Agent: t('roles.agent') };

/** One partner agency: profile and billing, its people, its machine keys. */
@Component({
  selector: 'app-admin-agency-detail',
  imports: [
    FormsModule, RouterLink, MoneyPipe, OdDatePipe, OdDateTimePipe, TPipe,
    Banner, ButtonDirective, Card, EmptyState, Field, Icon, InputDirective, PageHeader, Pill, Sheet, Skeleton,
    Tabs,
  ],
  templateUrl: './agency-detail.html',
  styleUrl: './agency-detail.scss',
})
export class AdminAgencyDetail {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly dialogs = inject(DialogService);
  private readonly toasts = inject(ToastService);

  readonly id = input.required<string>();

  readonly tab = signal<Tab>('overview');
  readonly busy = signal(false);
  readonly plans = PLANS;
  readonly planSummary = PLAN_SUMMARY;
  readonly error = signal<string | null>(null);
  readonly editOpen = signal(false);
  readonly billingOpen = signal(false);
  readonly userOpen = signal(false);
  readonly keyOpen = signal(false);
  readonly showPassword = signal(false);
  /** The secret shown exactly once after creating a key. */
  readonly createdKey = signal<ApiKeyCreatedDto | null>(null);
  readonly secretStored = signal(false);
  readonly copied = signal(false);

  readonly isAdmin = computed(() => this.auth.user()?.role === 'Admin');
  readonly initials = agencyInitials;

  readonly loaded = resource({
    params: () => ({ id: this.id() }),
    loader: ({ params }) => firstValueFrom(this.http.get<AgencyDetailDto>(`/api/v1/agencies/${params.id}`)),
  });
  readonly users = resource({
    params: () => ({ id: this.id() }),
    loader: ({ params }) => firstValueFrom(this.http.get<AgencyUserDto[]>(`/api/v1/agencies/${params.id}/users`)),
  });
  readonly keys = resource({
    params: () => ({ id: this.id() }),
    loader: ({ params }) => firstValueFrom(this.http.get<ApiKeyDto[]>(`/api/v1/agencies/${params.id}/api-keys`)),
  });
  readonly bookings = resource({
    params: () => ({ id: this.id() }),
    loader: ({ params }) => firstValueFrom(this.http.get<PagedResult<OperatorBookingSummary>>(
      '/api/v1/admin/bookings', { params: new HttpParams().set('agencyId', params.id).set('pageSize', 1) })),
  });

  readonly agency = computed(() => this.loaded.value() ?? null);
  readonly activeKeys = computed(() => (this.keys.value() ?? []).filter(k => apiKeyState(k) === 'Active').length);

  readonly tabs = computed<TabOption[]>(() => [
    { value: 'overview', label: t('agencyDetail.tabOverview') },
    { value: 'users', label: t('agencyDetail.users'), count: this.users.value()?.length ?? null },
    { value: 'keys', label: t('agencyDetail.tabKeys'), count: this.activeKeys() },
    { value: 'bookings', label: t('nav.bookings'), count: this.bookings.value()?.totalCount ?? null },
  ]);

  // Forms (template-driven; reset when their sheet opens).
  edit: UpdateAgencyRequest = { name: '', country: '', creditLimit: 0, contactEmail: '', status: 'Active' };
  billing: UpdateAgencyBillingRequest = { legalName: '', eik: '', vatNumber: '', address: '', city: '', mol: '' };
  user: CreateAgencyUserRequest = { email: '', fullName: '', role: 'Agent', password: '' };
  key: { name: string; expiry: KeyExpiry } = { name: '', expiry: '90d' };

  keyState = apiKeyState;

  roleLabel(role: string): string {
    return ROLE_LABELS[role] ?? role;
  }

  setTab(value: string): void {
    this.tab.set(value as Tab);
  }

  openEdit(): void {
    const a = this.agency();
    if (!a) return;
    this.edit = { name: a.name, country: a.country, creditLimit: a.creditLimit, contactEmail: a.contactEmail ?? '', status: a.status as 'Active' | 'Suspended' };
    this.error.set(null);
    this.editOpen.set(true);
  }

  async saveEdit(): Promise<void> {
    const a = this.agency();
    if (!a) return;
    await this.save(`/api/v1/agencies/${a.id}`, 'put', {
      ...this.edit, name: this.edit.name.trim(), country: this.edit.country.trim().toUpperCase(),
      contactEmail: this.edit.contactEmail?.trim() || null,
    }, t('agencyDetail.saved'), () => this.editOpen.set(false));
  }

  async toggleStatus(): Promise<void> {
    const a = this.agency();
    if (!a) return;
    const suspending = a.status === 'Active';
    const decision = await this.dialogs.confirm({
      title: t(suspending ? 'agencyDetail.suspendTitle' : 'agencyDetail.reactivateTitle', { name: a.name }),
      message: t(suspending ? 'agencyDetail.suspendMessage' : 'agencyDetail.reactivateMessage'),
      confirmLabel: t(suspending ? 'agencyDetail.suspendConfirm' : 'agencyDetail.reactivate'),
      destructive: suspending,
    });
    if (!decision) return;
    await this.save(`/api/v1/agencies/${a.id}`, 'put', {
      name: a.name, country: a.country, creditLimit: a.creditLimit, contactEmail: a.contactEmail,
      status: suspending ? 'Suspended' : 'Active',
    }, t(suspending ? 'agencyDetail.suspended' : 'agencyDetail.reactivated', { name: a.name }));
  }

  async pickPlan(event: Event): Promise<void> {
    const select = event.target as HTMLSelectElement;
    const current = this.agency()?.plan;
    // A declined confirmation leaves the model untouched, so the control has to be put back by hand.
    if (!(await this.setPlan(select.value as AgencyPlan)) && current) select.value = current;
  }

  async setPlan(plan: AgencyPlan): Promise<boolean> {
    const a = this.agency();
    if (!a || a.plan === plan) return false;
    const decision = await this.dialogs.confirm({
      title: t('agencyDetail.planTitle', { name: a.name, plan }),
      message: planChangeMessage(a.plan, plan),
      confirmLabel: t('agencyDetail.planConfirm', { plan }),
    });
    if (!decision) return false;
    await this.save(`/api/v1/agencies/${a.id}/plan`, 'put', { plan }, t('agencyDetail.planSet', { plan }));
    return true;
  }

  openBilling(): void {
    const a = this.agency();
    if (!a) return;
    const b = a.billing;
    this.billing = {
      legalName: b?.legalName ?? '', eik: b?.eik ?? '', vatNumber: b?.vatNumber ?? '',
      address: b?.address ?? '', city: b?.city ?? '', mol: b?.mol ?? '',
    };
    this.error.set(null);
    this.billingOpen.set(true);
  }

  billingValid(): boolean {
    const b = this.billing;
    return b.legalName.trim().length > 0 && /^\d{9}(\d{4})?$/.test(b.eik.trim())
      && (!b.vatNumber?.trim() || /^BG\d{9,10}$/.test(b.vatNumber.trim()))
      && b.address.trim().length > 0 && b.city.trim().length > 0 && b.mol.trim().length > 0;
  }

  async saveBilling(): Promise<void> {
    const a = this.agency();
    if (!a || !this.billingValid()) return;
    await this.save(`/api/v1/agencies/${a.id}/billing`, 'put', {
      legalName: this.billing.legalName.trim(), eik: this.billing.eik.trim(),
      vatNumber: this.billing.vatNumber?.trim() || null, address: this.billing.address.trim(),
      city: this.billing.city.trim(), mol: this.billing.mol.trim(),
    }, t('agencyDetail.billingSaved'), () => this.billingOpen.set(false));
  }

  openUser(): void {
    this.user = { email: '', fullName: '', role: 'Agent', password: '' };
    this.showPassword.set(false);
    this.error.set(null);
    this.userOpen.set(true);
  }

  userValid(): boolean {
    const u = this.user;
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(u.email.trim()) && u.fullName.trim().length > 0
      && u.password.length >= 12 && u.password.length <= 128;
  }

  async createUser(): Promise<void> {
    const a = this.agency();
    if (!a || !this.userValid()) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      const created = await firstValueFrom(this.http.post<AgencyUserDto>(`/api/v1/agencies/${a.id}/users`, {
        ...this.user, email: this.user.email.trim().toLowerCase(), fullName: this.user.fullName.trim(),
      }));
      this.userOpen.set(false);
      this.toasts.show(t('agencyDetail.userAdded', { name: created.fullName }));
      this.users.reload();
    } catch (err) {
      this.error.set(problemMessage(err, t('agencyDetail.userFailed')));
    } finally {
      this.busy.set(false);
    }
  }

  openKey(): void {
    this.key = { name: '', expiry: '90d' };
    this.error.set(null);
    this.keyOpen.set(true);
  }

  async createKey(): Promise<void> {
    const a = this.agency();
    if (!a || !this.key.name.trim()) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      const created = await firstValueFrom(this.http.post<ApiKeyCreatedDto>(`/api/v1/agencies/${a.id}/api-keys`, {
        name: this.key.name.trim(), expiresAt: keyExpiresAt(this.key.expiry),
      }));
      this.keyOpen.set(false);
      this.secretStored.set(false);
      this.copied.set(false);
      this.createdKey.set(created);
      this.keys.reload();
    } catch (err) {
      this.error.set(problemMessage(err, t('agencyDetail.keyFailed')));
    } finally {
      this.busy.set(false);
    }
  }

  async copySecret(): Promise<void> {
    const secret = this.createdKey()?.secret;
    if (!secret) return;
    try {
      await navigator.clipboard.writeText(secret);
      this.copied.set(true);
    } catch {
      this.toasts.show(t('agencyDetail.copyFailed'), { tone: 'bad' });
    }
  }

  finishKey(): void {
    const created = this.createdKey();
    this.createdKey.set(null);
    if (created) this.toasts.show(t('agencyDetail.keyCreated', { name: created.key.name }));
  }

  async revoke(key: ApiKeyDto): Promise<void> {
    const a = this.agency();
    if (!a) return;
    const decision = await this.dialogs.confirm({
      title: t('agencyDetail.revokeTitle', { name: key.name }),
      message: t('agencyDetail.revokeMessage', { agency: a.name }),
      confirmLabel: t('agencyDetail.revokeConfirm'),
      cancelLabel: t('agencyDetail.revokeKeep'),
      destructive: true,
    });
    if (!decision) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      await firstValueFrom(this.http.delete(`/api/v1/agencies/${a.id}/api-keys/${key.id}`));
      this.toasts.show(t('agencyDetail.revoked'));
      this.keys.reload();
    } catch (err) {
      this.error.set(problemMessage(err, t('agencyDetail.revokeFailed')));
    } finally {
      this.busy.set(false);
    }
  }

  private async save(url: string, method: 'put', body: unknown, done: string, after?: () => void): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await firstValueFrom(this.http[method]<AgencyDetailDto>(url, body));
      this.loaded.reload();
      this.toasts.show(done);
      after?.();
    } catch (err) {
      this.error.set(problemMessage(err, t('agencyDetail.saveFailed')));
    } finally {
      this.busy.set(false);
    }
  }
}
