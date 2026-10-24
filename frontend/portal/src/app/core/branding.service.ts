import { HttpClient } from '@angular/common/http';
import { DOCUMENT, Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { BrandingDto } from './api.types';

/** What the portal looks like until the deployment's branding has loaded (or if it never does). */
export const DEFAULT_BRANDING: BrandingDto = {
  displayName: 'Odisea',
  primaryColor: '#1f5eff',
  accentColor: '#1c2330',
  supportEmail: null,
  supportPhone: null,
  legalFooter: null,
  hasLogo: false,
  logoUrl: null,
  logoVersion: null,
  updatedAt: null,
};

/**
 * One portal build serves every operator: the name, colours and logo come from
 * GET /api/v1/branding at startup (anonymous, so the login page is branded too)
 * and land in the CSS tokens every component already uses.
 */
@Injectable({ providedIn: 'root' })
export class BrandingService {
  private readonly http = inject(HttpClient);
  private readonly document = inject(DOCUMENT);

  private readonly brandingSignal = signal<BrandingDto>(DEFAULT_BRANDING);
  readonly branding = this.brandingSignal.asReadonly();

  /** Logo URL with the content hash as cache-buster, or null when the operator has none. */
  readonly logoSrc = computed(() => {
    const b = this.brandingSignal();
    return b.hasLogo && b.logoUrl ? `${b.logoUrl}?v=${b.logoVersion ?? ''}` : null;
  });

  /** Never blocks startup: if the API is unreachable the portal keeps the defaults. */
  async load(): Promise<void> {
    try {
      this.apply(await firstValueFrom(this.http.get<BrandingDto>('/api/v1/branding')));
    } catch {
      this.apply(DEFAULT_BRANDING);
    }
  }

  apply(branding: BrandingDto): void {
    this.brandingSignal.set(branding);

    // The API validates colours as #rrggbb, so they are safe to hand to the style engine.
    const root = this.document.documentElement;
    root.style.setProperty('--accent', branding.primaryColor);
    root.style.setProperty('--accent-2', branding.accentColor);

    this.document.title = branding.displayName;
    const icon = this.document.querySelector<HTMLLinkElement>('link[rel="icon"]');
    if (icon) {
      icon.href = this.logoSrc() ?? 'favicon.ico';
    }
  }
}
