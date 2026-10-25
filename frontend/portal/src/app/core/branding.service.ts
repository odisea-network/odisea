import { HttpClient } from '@angular/common/http';
import { DOCUMENT, Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { BrandingDto } from './api.types';
import { BrandTheme, DEFAULT_ACCENT, DEFAULT_PRIMARY, deriveBrandTheme } from './theme';

/** What the portal looks like until the deployment's branding has loaded (or if it never does). */
export const DEFAULT_BRANDING: BrandingDto = {
  displayName: 'Odisea',
  primaryColor: DEFAULT_PRIMARY,
  accentColor: DEFAULT_ACCENT,
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
 * GET /api/v1/branding at startup (anonymous, so the login page is branded too).
 * The two brand colours become five CSS tokens — the inks are derived so text,
 * links and active navigation stay AA-readable whatever the operator picked.
 */
@Injectable({ providedIn: 'root' })
export class BrandingService {
  private readonly http = inject(HttpClient);
  private readonly document = inject(DOCUMENT);

  private readonly brandingSignal = signal<BrandingDto>(DEFAULT_BRANDING);
  readonly branding = this.brandingSignal.asReadonly();

  readonly theme = computed<BrandTheme>(() => {
    const b = this.brandingSignal();
    return deriveBrandTheme(b.primaryColor, b.accentColor);
  });

  /** Logo URL with the content hash as cache-buster, or null when the operator has none. */
  readonly logoSrc = computed(() => {
    const b = this.brandingSignal();
    return b.hasLogo && b.logoUrl ? `${b.logoUrl}?v=${b.logoVersion ?? ''}` : null;
  });

  /** First letter of the operator's name — the fallback mark when there is no logo. */
  readonly initial = computed(() => this.brandingSignal().displayName.trim().charAt(0).toUpperCase());

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

    // The API validates colours as #rrggbb; deriveBrandTheme falls back to the defaults otherwise.
    const theme = this.theme();
    const root = this.document.documentElement;
    root.style.setProperty('--primary', theme.primary);
    root.style.setProperty('--primary-ink', theme.primaryInk);
    root.style.setProperty('--on-primary', theme.onPrimary);
    root.style.setProperty('--accent', theme.accent);
    root.style.setProperty('--accent-ink', theme.accentInk);

    this.document.title = branding.displayName;
    const icon = this.document.querySelector<HTMLLinkElement>('link[rel="icon"]');
    if (icon) {
      icon.href = this.logoSrc() ?? 'favicon.ico';
    }
  }
}
