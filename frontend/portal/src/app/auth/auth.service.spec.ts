import { TestBed } from '@angular/core/testing';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService, CurrentUser, TokenResponse } from './auth.service';

const TOKEN: TokenResponse = {
  accessToken: 'jwt-abc',
  refreshToken: 'refresh-xyz',
  expiresAt: '2026-01-01T00:00:00Z',
};

const USER: CurrentUser = {
  id: 'u1',
  email: 'blue@blue-horizon.com',
  displayName: 'Blue Admin',
  role: 'AgencyAdmin',
  tenantType: 'Agency',
  tenantId: 'ag-1',
};

function makeHttp(overrides: Partial<Record<keyof HttpClient, unknown>> = {}) {
  return {
    post: vi.fn().mockReturnValue(of(TOKEN)),
    get: vi.fn().mockReturnValue(of(USER)),
    ...overrides,
  } as unknown as HttpClient;
}

describe('AuthService', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('starts unauthenticated with no stored token', () => {
    TestBed.configureTestingModule({ providers: [{ provide: HttpClient, useValue: makeHttp() }] });
    const auth = TestBed.inject(AuthService);

    expect(auth.isAuthenticated()).toBe(false);
    expect(auth.token()).toBeNull();
    expect(auth.user()).toBeNull();
  });

  it('login stores the token + user and flips isAuthenticated', async () => {
    const http = makeHttp();
    TestBed.configureTestingModule({ providers: [{ provide: HttpClient, useValue: http }] });
    const auth = TestBed.inject(AuthService);

    const user = await new Promise<CurrentUser>((resolve) =>
      auth.login({ email: 'x', password: 'y' }).subscribe(resolve),
    );

    expect(user).toEqual(USER);
    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.token()).toBe('jwt-abc');
    expect(auth.isAgency()).toBe(true);
    expect(auth.isOperator()).toBe(false);
    expect(localStorage.getItem('od_access_token')).toBe('jwt-abc');
  });

  it('logout clears token + user + storage', async () => {
    TestBed.configureTestingModule({ providers: [{ provide: HttpClient, useValue: makeHttp() }] });
    const auth = TestBed.inject(AuthService);
    await new Promise((r) => auth.login({ email: 'x', password: 'y' }).subscribe(r));

    auth.logout();

    expect(auth.isAuthenticated()).toBe(false);
    expect(auth.user()).toBeNull();
    expect(localStorage.getItem('od_access_token')).toBeNull();
    expect(localStorage.getItem('od_current_user')).toBeNull();
  });

  it('rehydrates token + user from localStorage on construction', () => {
    localStorage.setItem('od_access_token', 'persisted-jwt');
    localStorage.setItem('od_current_user', JSON.stringify(USER));

    TestBed.configureTestingModule({ providers: [{ provide: HttpClient, useValue: makeHttp() }] });
    const auth = TestBed.inject(AuthService);

    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.token()).toBe('persisted-jwt');
    expect(auth.user()?.role).toBe('AgencyAdmin');
  });

  it('identifies an operator tenant', async () => {
    const operator: CurrentUser = { ...USER, tenantType: 'Operator', role: 'OperatorAdmin' };
    const http = makeHttp({ get: vi.fn().mockReturnValue(of(operator)) });
    TestBed.configureTestingModule({ providers: [{ provide: HttpClient, useValue: http }] });
    const auth = TestBed.inject(AuthService);

    await new Promise((r) => auth.login({ email: 'x', password: 'y' }).subscribe(r));

    expect(auth.isOperator()).toBe(true);
    expect(auth.isAgency()).toBe(false);
  });
});
