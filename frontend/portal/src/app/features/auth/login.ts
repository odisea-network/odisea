import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  template: `
    <div class="login-page">
      <form class="login-card" (ngSubmit)="submit()">
        <h1>Odisea</h1>
        <p class="subtitle">B2B travel platform</p>

        <label for="email">Email</label>
        <input id="email" name="email" type="email" required autocomplete="username"
               [(ngModel)]="email" [disabled]="busy()" />

        <label for="password">Password</label>
        <input id="password" name="password" type="password" required autocomplete="current-password"
               [(ngModel)]="password" [disabled]="busy()" />

        @if (error()) {
          <p class="error" role="alert">{{ error() }}</p>
        }

        <button type="submit" [disabled]="busy() || !email || !password">
          {{ busy() ? 'Signing in…' : 'Sign in' }}
        </button>
      </form>
    </div>
  `,
  styles: `
    .login-page { min-height: 100dvh; display: grid; place-items: center; background: var(--surface-2); }
    .login-card {
      display: flex; flex-direction: column; gap: .5rem;
      width: min(22rem, 90vw); padding: 2rem; border-radius: .75rem;
      background: var(--surface-1); box-shadow: 0 8px 30px rgb(0 0 0 / .08);
    }
    h1 { margin: 0; font-size: 1.6rem; }
    .subtitle { margin: 0 0 1rem; color: var(--text-muted); font-size: .9rem; }
    label { font-size: .8rem; font-weight: 600; }
    .error { color: var(--danger); font-size: .85rem; margin: .25rem 0 0; }
    button { margin-top: 1rem; }
  `,
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  email = '';
  password = '';
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  async submit(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      const user = await this.auth.login(this.email, this.password);
      await this.router.navigate([user.userType === 'agency' ? '/agency' : '/admin']);
    } catch (err) {
      this.error.set(
        err instanceof HttpErrorResponse && (err.status === 401 || err.status === 423)
          ? ((err.error?.detail as string) ?? 'Invalid email or password.')
          : 'Login failed — is the API running?',
      );
    } finally {
      this.busy.set(false);
    }
  }
}
