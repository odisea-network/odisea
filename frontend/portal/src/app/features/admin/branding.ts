import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { BrandingDto, UpdateBrandingRequest } from '../../core/api.types';
import { AuthService } from '../../core/auth.service';
import { BrandingService } from '../../core/branding.service';
import { problemMessage } from '../../core/problem';
import { contrastRatio, deriveBrandTheme } from '../../core/theme';
import {
  Banner, ButtonDirective, Card, DialogService, Field, Icon, InputDirective, PageHeader, Pill, ToastService,
} from '../../shared/ui';

const PRIMARY_SWATCHES = ['#1946B8', '#0B3B5C', '#C2410C', '#0F5E57', '#5B2A86', '#F2C94C'];
const ACCENT_SWATCHES = ['#E8A23A', '#0E8C83', '#D9467A', '#3B82F6', '#65A30D'];
const MAX_LOGO_BYTES = 512 * 1024;

/** Settings › Branding: how the portal looks to agencies and staff, previewed before publishing. */
@Component({
  selector: 'app-admin-branding',
  imports: [FormsModule, Banner, ButtonDirective, Card, Field, Icon, InputDirective, PageHeader, Pill],
  templateUrl: './branding.html',
  styleUrl: './branding.scss',
})
export class AdminBranding {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly dialogs = inject(DialogService);
  private readonly toasts = inject(ToastService);
  protected readonly brand = inject(BrandingService);

  readonly isAdmin = computed(() => this.auth.user()?.role === 'Admin');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly primarySwatches = PRIMARY_SWATCHES;
  readonly accentSwatches = ACCENT_SWATCHES;

  // Draft values; the live tokens only change on publish.
  readonly displayName = signal('');
  readonly primary = signal('#1946b8');
  readonly accent = signal('#e8a23a');
  readonly supportEmail = signal('');
  readonly supportPhone = signal('');
  readonly legalFooter = signal('');

  readonly preview = computed(() => deriveBrandTheme(this.primary(), this.accent()));
  readonly initial = computed(() => this.displayName().trim().charAt(0).toUpperCase() || '?');
  readonly whiteOnPrimary = computed(() => contrastRatio(this.primary(), '#ffffff'));
  readonly dirty = computed(() => {
    const b = this.brand.branding();
    return this.displayName() !== b.displayName || this.primary().toLowerCase() !== b.primaryColor.toLowerCase()
      || this.accent().toLowerCase() !== b.accentColor.toLowerCase() || this.supportEmail() !== (b.supportEmail ?? '')
      || this.supportPhone() !== (b.supportPhone ?? '') || this.legalFooter() !== (b.legalFooter ?? '');
  });
  readonly valid = computed(() => this.displayName().trim().length > 0 && /^#[0-9a-fA-F]{6}$/.test(this.primary()) && /^#[0-9a-fA-F]{6}$/.test(this.accent()));

  constructor() {
    this.reset();
  }

  reset(): void {
    const b = this.brand.branding();
    this.displayName.set(b.displayName);
    this.primary.set(b.primaryColor.toLowerCase());
    this.accent.set(b.accentColor.toLowerCase());
    this.supportEmail.set(b.supportEmail ?? '');
    this.supportPhone.set(b.supportPhone ?? '');
    this.legalFooter.set(b.legalFooter ?? '');
    this.error.set(null);
  }

  async publish(): Promise<void> {
    if (!this.valid() || !this.isAdmin()) return;
    const request: UpdateBrandingRequest = {
      displayName: this.displayName().trim(),
      primaryColor: this.primary(),
      accentColor: this.accent(),
      supportEmail: this.supportEmail().trim() || null,
      supportPhone: this.supportPhone().trim() || null,
      legalFooter: this.legalFooter().trim() || null,
    };
    await this.run(async () => {
      const saved = await firstValueFrom(this.http.put<BrandingDto>('/api/v1/branding', request));
      this.brand.apply(saved);
      this.toasts.show('Branding published — every page now uses it');
    });
  }

  async uploadLogo(event: Event): Promise<void> {
    const inputEl = event.target as HTMLInputElement;
    const file = inputEl.files?.[0];
    inputEl.value = '';
    if (!file) return;
    if (file.size > MAX_LOGO_BYTES) {
      this.error.set('The logo must be at most 512 KB.');
      return;
    }
    const form = new FormData();
    form.append('file', file, file.name);
    await this.run(async () => {
      const saved = await firstValueFrom(this.http.put<BrandingDto>('/api/v1/branding/logo', form));
      this.brand.apply(saved);
      this.toasts.show('Logo updated');
    });
  }

  async removeLogo(): Promise<void> {
    const decision = await this.dialogs.confirm({
      title: 'Remove the logo?',
      message: 'The portal falls back to the first letter of the display name.',
      confirmLabel: 'Remove logo',
      destructive: true,
    });
    if (!decision) return;
    await this.run(async () => {
      const saved = await firstValueFrom(this.http.delete<BrandingDto>('/api/v1/branding/logo'));
      this.brand.apply(saved);
      this.toasts.show('Logo removed');
    });
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await action();
    } catch (err) {
      this.error.set(problemMessage(err, 'The change was not saved.'));
    } finally {
      this.busy.set(false);
    }
  }
}
