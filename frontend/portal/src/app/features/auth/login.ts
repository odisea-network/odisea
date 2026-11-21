import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { BrandingService } from '../../core/branding.service';
import { t } from '../../core/i18n/i18n';
import { TPipe } from '../../shared/pipes/t.pipe';
import { Banner, ButtonDirective, Field, Icon, InputDirective } from '../../shared/ui';

interface LoginError {
  tone: 'bad' | 'warn';
  message: string;
}

@Component({
  selector: 'app-login',
  imports: [FormsModule, Banner, ButtonDirective, Field, Icon, InputDirective, TPipe],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly brand = inject(BrandingService);

  email = '';
  password = '';
  readonly busy = signal(false);
  readonly showPassword = signal(false);
  readonly error = signal<LoginError | null>(null);

  async submit(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      const user = await this.auth.login(this.email, this.password);
      await this.router.navigate([user.userType === 'agency' ? '/agency' : '/admin']);
    } catch (err) {
      this.error.set(describe(err));
    } finally {
      this.busy.set(false);
    }
  }
}

/** Same wording for unknown email and wrong password — the API never says which. */
function describe(err: unknown): LoginError {
  if (err instanceof HttpErrorResponse) {
    if (err.status === 423) return { tone: 'warn', message: t('login.locked') };
    if (err.status === 401) return { tone: 'bad', message: t('login.invalid') };
  }
  return { tone: 'bad', message: t('login.unreachable') };
}
