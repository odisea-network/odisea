import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { BrandingDto } from './api.types';
import { BrandingService, DEFAULT_BRANDING } from './branding.service';

const paradise: BrandingDto = {
  displayName: 'Paradise Travel',
  primaryColor: '#0a7c86',
  accentColor: '#f2a900',
  supportEmail: 'office@paradise.example',
  supportPhone: null,
  legalFooter: 'Paradise Travel Ltd',
  hasLogo: true,
  logoUrl: '/api/v1/branding/logo',
  logoVersion: 'ae9e3aef9225',
  updatedAt: '2026-10-24T12:00:00Z',
};

describe('BrandingService', () => {
  let service: BrandingService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(BrandingService);
    http = TestBed.inject(HttpTestingController);
    document.documentElement.style.removeProperty('--accent');
    document.documentElement.style.removeProperty('--accent-2');
  });

  it('applies the deployment branding to the tokens, the title and the logo', async () => {
    const loading = service.load();
    http.expectOne('/api/v1/branding').flush(paradise);
    await loading;

    expect(service.branding().displayName).toBe('Paradise Travel');
    expect(document.documentElement.style.getPropertyValue('--accent')).toBe('#0a7c86');
    expect(document.documentElement.style.getPropertyValue('--accent-2')).toBe('#f2a900');
    expect(document.title).toBe('Paradise Travel');
    expect(service.logoSrc()).toBe('/api/v1/branding/logo?v=ae9e3aef9225');
  });

  it('keeps the defaults when the API cannot be reached', async () => {
    const loading = service.load();
    http.expectOne('/api/v1/branding').flush('down', { status: 503, statusText: 'Unavailable' });
    await loading;

    expect(service.branding()).toEqual(DEFAULT_BRANDING);
    expect(service.logoSrc()).toBeNull();
    expect(document.documentElement.style.getPropertyValue('--accent')).toBe('#1f5eff');
  });

  it('has no logo source when the operator has not uploaded one', () => {
    service.apply({ ...paradise, hasLogo: false, logoUrl: null, logoVersion: null });
    expect(service.logoSrc()).toBeNull();
  });
});
