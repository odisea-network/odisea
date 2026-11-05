import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { CompanyProfileDto } from '../../core/api.types';
import { problemMessage } from '../../core/problem';
import {
  Banner, ButtonDirective, Card, Field, InputDirective, PageHeader, Skeleton, ToastService,
} from '../../shared/ui';
import { SettingsNav } from './settings-nav';

function emptyProfile(): CompanyProfileDto {
  return { name: '', eik: '', vatNumber: '', address: '', city: '', mol: '', iban: '', bankName: '' };
}

/** Settings › Company: the operator's own requisites, printed as the supplier on every invoice. */
@Component({
  selector: 'app-admin-company',
  imports: [FormsModule, Banner, ButtonDirective, Card, Field, InputDirective, PageHeader, Skeleton, SettingsNav],
  templateUrl: './company.html',
  styleUrl: './company.scss',
})
export class AdminCompany {
  private readonly http = inject(HttpClient);
  private readonly toasts = inject(ToastService);

  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly isNew = signal(false);

  readonly loaded = resource({
    loader: async () => {
      try {
        const profile = await firstValueFrom(this.http.get<CompanyProfileDto>('/api/v1/company-profile'));
        this.form = { ...profile, vatNumber: profile.vatNumber ?? '', iban: profile.iban ?? '', bankName: profile.bankName ?? '' };
        this.isNew.set(false);
        return profile;
      } catch (err) {
        // 404 just means nothing has been saved yet.
        if (err instanceof HttpErrorResponse && err.status === 404) {
          this.form = emptyProfile();
          this.isNew.set(true);
          return null;
        }
        throw err;
      }
    },
  });

  form: CompanyProfileDto = emptyProfile();

  valid(): boolean {
    const f = this.form;
    return f.name.trim().length > 0 && /^\d{9}(\d{4})?$/.test(f.eik.trim())
      && (!f.vatNumber?.trim() || /^BG\d{9,10}$/.test(f.vatNumber.trim()))
      && f.address.trim().length > 0 && f.city.trim().length > 0 && f.mol.trim().length > 0
      && (!f.iban?.trim() || /^[A-Z]{2}\d{2}[A-Z0-9]{10,30}$/.test(f.iban.trim().replace(/\s+/g, '').toUpperCase()));
  }

  async save(): Promise<void> {
    if (!this.valid()) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      const f = this.form;
      await firstValueFrom(this.http.put<CompanyProfileDto>('/api/v1/company-profile', {
        name: f.name.trim(), eik: f.eik.trim(), vatNumber: f.vatNumber?.trim() || null,
        address: f.address.trim(), city: f.city.trim(), mol: f.mol.trim(),
        iban: f.iban?.trim() ? f.iban.trim().replace(/\s+/g, '').toUpperCase() : null,
        bankName: f.bankName?.trim() || null,
      }));
      this.toasts.show('Company profile saved');
      this.loaded.reload();
    } catch (err) {
      this.error.set(problemMessage(err, 'The profile was not saved.'));
    } finally {
      this.busy.set(false);
    }
  }
}
