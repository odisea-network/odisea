import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { AuthService, decodeJwtPayload } from './auth.service';

function fakeJwt(claims: Record<string, unknown>): string {
  const encode = (value: unknown) =>
    btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `${encode({ alg: 'HS256' })}.${encode(claims)}.signature`;
}

function sessionBody(overrides: Partial<Record<string, unknown>> = {}) {
  const exp = Math.floor(Date.now() / 1000) + 900;
  return {
    accessToken: fakeJwt({ email: 'agent@odisea.local', exp }),
    expiresAt: new Date(exp * 1000).toISOString(),
    userType: 'agency',
    role: 'AgencyAdmin',
    agencyId: 'a-1',
    fullName: 'Demo Agent',
    ...overrides,
  };
}

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  it('starts signed out and keeps the token in memory only', async () => {
    expect(service.isAuthenticated()).toBe(false);

    const pending = service.login('agent@odisea.local', 'secret-password');
    http.expectOne('/api/v1/auth/login').flush(sessionBody());
    await pending;

    expect(service.isAuthenticated()).toBe(true);
    expect(service.token).not.toBeNull();
    // No script-readable persistence anywhere.
    expect(sessionStorage.length).toBe(0);
    expect(localStorage.length).toBe(0);
  });

  it('tryRestore establishes a session from the refresh cookie', async () => {
    const pending = service.tryRestore();
    http.expectOne('/api/v1/auth/refresh').flush(sessionBody());
    await pending;

    expect(service.isAuthenticated()).toBe(true);
    expect(service.user()?.userType).toBe('agency');
  });

  it('tryRestore failure leaves the user signed out without throwing', async () => {
    const pending = service.tryRestore();
    http.expectOne('/api/v1/auth/refresh').flush(
      { title: 'No session' }, { status: 401, statusText: 'Unauthorized' });
    await pending;

    expect(service.isAuthenticated()).toBe(false);
    expect(service.token).toBeNull();
  });

  it('concurrent refreshes share one request (rotation is single-use)', async () => {
    const first = service.refreshOnce();
    const second = service.refreshOnce();

    http.expectOne('/api/v1/auth/refresh').flush(sessionBody());
    const [a, b] = await Promise.all([first, second]);

    expect(a).toBe(b);
    http.verify(); // exactly one request hit the wire
  });

  it('logout clears state and revokes server-side', async () => {
    const pending = service.login('a@b.c', 'x-password-123');
    http.expectOne('/api/v1/auth/login').flush(sessionBody());
    await pending;

    service.logout();
    http.expectOne('/api/v1/auth/logout').flush(null, { status: 204, statusText: 'No Content' });

    expect(service.isAuthenticated()).toBe(false);
    expect(service.token).toBeNull();
  });

  it('decodeJwtPayload survives garbage', () => {
    expect(decodeJwtPayload('not-a-token')).toEqual({});
  });
});
