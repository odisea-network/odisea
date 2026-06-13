import { TestBed } from '@angular/core/testing';
import { HttpHandlerFn, HttpRequest } from '@angular/common/http';
import { EMPTY } from 'rxjs';
import { describe, expect, it } from 'vitest';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

function interceptedRequest(token: string | null, url: string): HttpRequest<unknown> {
  const auth = { token: () => token } as unknown as AuthService;
  TestBed.configureTestingModule({ providers: [{ provide: AuthService, useValue: auth }] });

  let captured!: HttpRequest<unknown>;
  const next: HttpHandlerFn = (req) => {
    captured = req;
    return EMPTY;
  };

  // TestBed gives us an injection context so the interceptor's inject() works.
  TestBed.runInInjectionContext(() => {
    authInterceptor(new HttpRequest('GET', url), next);
  });
  return captured;
}

describe('authInterceptor', () => {
  it('attaches the bearer token to API requests when present', () => {
    const req = interceptedRequest('jwt-abc', '/api/v1/offers/mine');
    expect(req.headers.get('Authorization')).toBe('Bearer jwt-abc');
  });

  it('does not attach a header when there is no token', () => {
    const req = interceptedRequest(null, '/api/v1/offers/mine');
    expect(req.headers.has('Authorization')).toBe(false);
  });

  it('skips the login endpoint even with a token', () => {
    const req = interceptedRequest('jwt-abc', '/api/v1/auth/login');
    expect(req.headers.has('Authorization')).toBe(false);
  });

  it('skips non-API URLs', () => {
    const req = interceptedRequest('jwt-abc', 'https://cdn.example/asset.js');
    expect(req.headers.has('Authorization')).toBe(false);
  });
});
