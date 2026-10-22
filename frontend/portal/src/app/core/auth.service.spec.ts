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

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  it('starts signed out', () => {
    expect(service.isAuthenticated()).toBe(false);
    expect(service.token).toBeNull();
  });

  it('login stores the token, decodes the user and persists the session', async () => {
    const exp = Math.floor(Date.now() / 1000) + 3600;
    const token = fakeJwt({ email: 'agent@odisea.local', exp });

    const pending = service.login('agent@odisea.local', 'secret-password');
    const request = http.expectOne('/api/v1/auth/login');
    expect(request.request.method).toBe('POST');
    request.flush({
      accessToken: token,
      expiresAt: new Date(exp * 1000).toISOString(),
      userType: 'agency',
      role: 'AgencyAdmin',
      agencyId: 'a-1',
      fullName: 'Demo Agent',
    });

    const user = await pending;
    expect(user.userType).toBe('agency');
    expect(user.email).toBe('agent@odisea.local');
    expect(service.isAuthenticated()).toBe(true);
    expect(service.token).toBe(token);
    expect(sessionStorage.getItem('odisea.session')).toContain('agency');
  });

  it('logout clears everything', async () => {
    const exp = Math.floor(Date.now() / 1000) + 3600;
    const pending = service.login('a@b.c', 'x-password-123');
    http.expectOne('/api/v1/auth/login').flush({
      accessToken: fakeJwt({ exp }),
      expiresAt: '',
      userType: 'operator',
      role: 'Admin',
      agencyId: null,
      fullName: 'Admin',
    });
    await pending;

    service.logout();
    expect(service.isAuthenticated()).toBe(false);
    expect(service.token).toBeNull();
    expect(sessionStorage.getItem('odisea.session')).toBeNull();
  });

  it('an expired session is not authenticated', async () => {
    const pending = service.login('a@b.c', 'x-password-123');
    http.expectOne('/api/v1/auth/login').flush({
      accessToken: fakeJwt({ exp: Math.floor(Date.now() / 1000) - 10 }),
      expiresAt: '',
      userType: 'agency',
      role: 'Agent',
      agencyId: 'a-1',
      fullName: 'A',
    });
    await pending;

    expect(service.isAuthenticated()).toBe(false);
  });

  it('decodeJwtPayload survives garbage', () => {
    expect(decodeJwtPayload('not-a-token')).toEqual({});
  });
});
