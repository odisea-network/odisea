import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="login">
      <h2>Sign in</h2>
      @if (error()) {
        <p class="error">{{ error() }}</p>
      }
      <form (ngSubmit)="submit()">
        <label>
          Email
          <input name="email" type="email" [(ngModel)]="email" autocomplete="username" required />
        </label>
        <label>
          Password
          <input name="password" type="password" [(ngModel)]="password" autocomplete="current-password" required />
        </label>
        <button type="submit" [disabled]="loading() || !email || !password">
          {{ loading() ? 'Signing in…' : 'Sign in' }}
        </button>
      </form>
      <p class="hint">Dev: blue&#64;blue-horizon.com / Blue1234! · ops&#64;sun-operators.com / Ops1234!</p>
    </div>
  `,
  styles: [
    `
      .login { max-width: 360px; margin: 48px auto; }
      form { display: flex; flex-direction: column; gap: 14px; }
      label { display: flex; flex-direction: column; gap: 4px; font-size: 0.9rem; color: #444; }
      input { padding: 9px 10px; border: 1px solid #ccc; border-radius: 6px; font-size: 1rem; }
      button { padding: 10px; border: 0; border-radius: 6px; background: #0a2540; color: #fff; font-size: 1rem; cursor: pointer; }
      button:disabled { opacity: 0.5; cursor: default; }
      .error { color: #c00; }
      .hint { margin-top: 18px; color: #888; font-size: 0.8rem; }
    `,
  ],
})
export class LoginPage {
  private auth = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  email = '';
  password = '';
  loading = signal(false);
  error = signal<string | null>(null);

  submit(): void {
    if (!this.email || !this.password) return;
    this.loading.set(true);
    this.error.set(null);

    this.auth.login({ email: this.email, password: this.password }).subscribe({
      next: () => {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/offers';
        this.router.navigateByUrl(returnUrl);
      },
      error: () => {
        this.error.set('Invalid email or password.');
        this.loading.set(false);
      },
    });
  }
}
