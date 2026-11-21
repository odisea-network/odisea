import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, UrlTree, provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';
import { AgencyPlanDto } from './api.types';
import { AuthService, SessionUser } from './auth.service';
import { EntitlementsService, featureGuard } from './entitlements.service';

class AuthStub {
  readonly user = signal<SessionUser | null>(null);
}

const agent = (agencyId: string): SessionUser => ({
  userType: 'agency', role: 'Agent', agencyId, fullName: 'A', email: 'a@b.c', expiresAtEpoch: 0,
});

const plan = (agencyId: string, features: AgencyPlanDto['features'], isActive = true): AgencyPlanDto => ({
  agencyId, plan: features.length ? 'Ledger' : 'Core', isActive, features,
});

describe('EntitlementsService', () => {
  let auth: AuthStub;
  let http: HttpTestingController;
  let entitlements: EntitlementsService;

  beforeEach(() => {
    auth = new AuthStub();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
        { provide: AuthService, useValue: auth },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    entitlements = TestBed.inject(EntitlementsService);
  });

  const guard = () =>
    TestBed.runInInjectionContext(() => featureGuard('ReservationLedger')(null!, null!)) as Promise<boolean | UrlTree>;

  it('asks nothing for an operator and includes nothing', async () => {
    auth.user.set({ ...agent('x'), userType: 'operator', agencyId: null });
    expect(await entitlements.load()).toBeNull();
    http.expectNone('/api/v1/agency/plan');
    expect(entitlements.has('ReservationLedger')).toBe(false);
  });

  it('loads the plan once and answers from it', async () => {
    auth.user.set(agent('a-1'));
    const first = entitlements.load();
    const second = entitlements.load();
    http.expectOne('/api/v1/agency/plan').flush(plan('a-1', ['ReservationLedger']));
    await Promise.all([first, second]);

    expect(entitlements.has('ReservationLedger')).toBe(true);
    expect(entitlements.has('InquiryForms')).toBe(false);
  });

  it('forgets the plan when another agency signs in', async () => {
    auth.user.set(agent('a-1'));
    const loading = entitlements.load();
    http.expectOne('/api/v1/agency/plan').flush(plan('a-1', ['ReservationLedger']));
    await loading;

    auth.user.set(agent('a-2'));
    expect(entitlements.has('ReservationLedger')).toBe(false);
    const reloading = entitlements.load();
    http.expectOne('/api/v1/agency/plan').flush(plan('a-2', []));
    await reloading;
    expect(entitlements.has('ReservationLedger')).toBe(false);
  });

  it('includes nothing for a suspended agency or after a failed load, and retries next time', async () => {
    auth.user.set(agent('a-1'));
    const failing = entitlements.load();
    http.expectOne('/api/v1/agency/plan').flush('down', { status: 503, statusText: 'Unavailable' });
    expect(await failing).toBeNull();
    expect(entitlements.has('ReservationLedger')).toBe(false);

    const retry = entitlements.load();
    http.expectOne('/api/v1/agency/plan').flush(plan('a-1', ['ReservationLedger'], false));
    await retry;
    expect(entitlements.has('ReservationLedger')).toBe(false);
  });

  it('guards a route by feature', async () => {
    auth.user.set(agent('a-1'));
    const allowed = guard();
    http.expectOne('/api/v1/agency/plan').flush(plan('a-1', ['ReservationLedger']));
    expect(await allowed).toBe(true);

    auth.user.set(agent('a-2'));
    const refused = guard();
    http.expectOne('/api/v1/agency/plan').flush(plan('a-2', []));
    const result = await refused;
    expect(result).toBeInstanceOf(UrlTree);
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/agency');
  });
});
