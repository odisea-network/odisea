import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from './auth.service';
import { LoginPage } from './login.page';

function make(view: 'signin' | 'signup' = 'signin', api: Partial<Record<keyof AuthService, unknown>> = {}) {
  const router = { navigateByUrl: vi.fn() };
  const route = { snapshot: { data: { view }, queryParamMap: { get: () => null } } };
  const auth = {
    login: vi.fn().mockReturnValue(of({ id: '1', email: 'a@b.c', displayName: 'A', role: 'AgencyAdmin', tenantType: 'Agency', tenantId: 'x' })),
    register: vi.fn().mockReturnValue(of({ id: '1', email: 'a@b.c', displayName: 'A', role: 'AgencyAdmin', tenantType: 'Agency', tenantId: 'x' })),
    ...api,
  };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      { provide: AuthService, useValue: auth },
      { provide: Router, useValue: router },
      { provide: ActivatedRoute, useValue: route },
    ],
  });
  const page = TestBed.runInInjectionContext(() => new LoginPage());
  return { page, router, auth };
}

describe('LoginPage (auth)', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    localStorage.clear();
  });

  it('starts on the signin view by default and signup when routed', () => {
    expect(make('signin').page.view()).toBe('signin');
    expect(make('signup').page.view()).toBe('signup');
  });

  it('toggles language and persists it', () => {
    const { page } = make();
    expect(page.lang()).toBe('bg');
    expect(page.t().signin.submit).toBe('Вход');
    page.toggleLang();
    expect(page.lang()).toBe('en');
    expect(page.t().signin.submit).toBe('Sign in');
    expect(localStorage.getItem('odisea_lang')).toBe('en');
  });

  it('go() switches views and clears transient state', () => {
    const { page } = make();
    page.sent.set(true);
    page.go('signup');
    expect(page.view()).toBe('signup');
    expect(page.sent()).toBe(false);
  });

  it('submitSignin logs in and navigates to the return url', () => {
    const { page, router, auth } = make();
    page.email = 'maria@x.bg';
    page.password = 'Password1!';
    page.submitSignin();

    expect(auth.login).toHaveBeenCalledWith({ email: 'maria@x.bg', password: 'Password1!' });
    expect(router.navigateByUrl).toHaveBeenCalledWith('/offers');
  });

  it('submitSignin surfaces an error on failure', () => {
    const { page } = make('signin', { login: vi.fn().mockReturnValue(throwError(() => new Error('nope'))) });
    page.email = 'x@y.z';
    page.password = 'bad';
    page.submitSignin();
    expect(page.error()).toBe('Невалиден имейл или парола.');
  });

  it('submitSignup is gated by the terms checkbox', () => {
    const { page, auth } = make('signup');
    page.name = 'Мария';
    page.email = 'maria@x.bg';
    page.password = 'Password1!';
    page.agency = 'Слънчев Тур';

    page.submitSignup(); // agree() is false
    expect(auth.register).not.toHaveBeenCalled();

    page.agree.set(true);
    page.submitSignup();
    expect(auth.register).toHaveBeenCalledWith(expect.objectContaining({
      displayName: 'Мария', tenantName: 'Слънчев Тур', tenantRole: 'agency',
    }));
  });

  it('submitForgot shows the sent confirmation', () => {
    const { page } = make();
    page.go('forgot');
    page.submitForgot();
    expect(page.sent()).toBe(true);
  });
});
