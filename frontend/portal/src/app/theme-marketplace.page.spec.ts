import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiService, ThemeDto } from './api.service';
import { ThemeMarketplacePage } from './theme-marketplace.page';

const preset = (id: string, name: string): ThemeDto => ({
  id, agencyId: '00000000-0000-0000-0000-000000000000', name, status: 'Published', version: 1,
  isPreset: true,
  tokens: { foundation: { accent: '#1a7f8c' }, semantic: { bg: '#f3fafb', price: '#0e1618', surface: '#fff' }, component: {} },
});

function setup(api: Partial<Record<keyof ApiService, unknown>> = {}, router: Partial<Router> = {}) {
  const apiMock = {
    listPresets: vi.fn().mockReturnValue(of([preset('p1', 'Coastal'), preset('p2', 'Sunset')])),
    cloneFromPreset: vi.fn().mockReturnValue(of({ ...preset('new-1', 'Coastal (copy)'), isPreset: false })),
    ...api,
  } as unknown as ApiService;
  const routerMock = { navigate: vi.fn(), ...router } as unknown as Router;

  TestBed.configureTestingModule({
    providers: [
      { provide: ApiService, useValue: apiMock },
      { provide: Router, useValue: routerMock },
    ],
  });
  const page = TestBed.runInInjectionContext(() => new ThemeMarketplacePage());
  return { page, apiMock, routerMock };
}

describe('ThemeMarketplacePage', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('loads presets on construction', () => {
    const { page, apiMock } = setup();
    expect(apiMock.listPresets).toHaveBeenCalled();
    expect(page.presets().length).toBe(2);
    expect(page.loading()).toBe(false);
  });

  it('reads a token via the helper with a safe fallback', () => {
    const { page } = setup();
    const p = page.presets()[0];
    expect(page.token(p, 'foundation', 'accent')).toBe('#1a7f8c');
    expect(page.token(p, 'component', 'missing')).toBe('');
  });

  it('use() clones the preset then navigates to the new theme in the studio', async () => {
    const { page, apiMock, routerMock } = setup();

    page.use(preset('p1', 'Coastal'));

    expect(apiMock.cloneFromPreset).toHaveBeenCalledWith('p1');
    expect(routerMock.navigate).toHaveBeenCalledWith(['/themes', 'new-1']);
  });

  it('surfaces an error when cloning fails and clears the spinner', () => {
    const { page } = setup({
      cloneFromPreset: vi.fn().mockReturnValue(throwError(() => ({ error: { detail: 'nope' } }))),
    });

    page.use(preset('p1', 'Coastal'));

    expect(page.error()).toBe('nope');
    expect(page.cloning()).toBeNull();
  });
});
