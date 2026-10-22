import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';
import { AuthService, SessionUser } from './auth.service';
import { adminGuard, agencyGuard } from './guards';

class AuthStub {
  userValue: SessionUser | null = null;
  isAuthenticated = () => this.userValue !== null;
  user = () => this.userValue;
}

function run(guard: typeof agencyGuard): boolean | UrlTree {
  return TestBed.runInInjectionContext(() => guard(null!, null!)) as boolean | UrlTree;
}

describe('role guards', () => {
  let auth: AuthStub;

  beforeEach(() => {
    auth = new AuthStub();
    TestBed.configureTestingModule({
      providers: [{ provide: AuthService, useValue: auth }],
    });
  });

  it('redirects anonymous users to login', () => {
    const result = run(agencyGuard);
    expect(result).toBeInstanceOf(UrlTree);
    expect(result.toString()).toBe('/login');
  });

  it('lets an agency user into /agency but not /admin', () => {
    auth.userValue = {
      userType: 'agency', role: 'Agent', agencyId: 'a-1',
      fullName: 'A', email: 'a@b.c', expiresAtEpoch: 0,
    };
    expect(run(agencyGuard)).toBe(true);
    expect(run(adminGuard)).toBeInstanceOf(UrlTree);
  });

  it('lets an operator into /admin but not /agency', () => {
    auth.userValue = {
      userType: 'operator', role: 'Admin', agencyId: null,
      fullName: 'O', email: 'o@b.c', expiresAtEpoch: 0,
    };
    expect(run(adminGuard)).toBe(true);
    expect(run(agencyGuard)).toBeInstanceOf(UrlTree);
  });
});
