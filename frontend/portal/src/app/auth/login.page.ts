import { Component, computed, inject, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { AuthService } from './auth.service';

const GOOGLE_SVG = '<svg width="18" height="18" viewBox="0 0 18 18"><path fill="#4285F4" d="M17.64 9.2c0-.64-.06-1.25-.16-1.84H9v3.48h4.84a4.14 4.14 0 0 1-1.8 2.72v2.26h2.92c1.7-1.57 2.68-3.88 2.68-6.62z"/><path fill="#34A853" d="M9 18c2.43 0 4.47-.8 5.96-2.18l-2.92-2.26c-.8.54-1.84.86-3.04.86-2.34 0-4.32-1.58-5.03-3.7H.96v2.33A9 9 0 0 0 9 18z"/><path fill="#FBBC05" d="M3.97 10.72a5.4 5.4 0 0 1 0-3.44V4.95H.96a9 9 0 0 0 0 8.1l3.01-2.33z"/><path fill="#EA4335" d="M9 3.58c1.32 0 2.5.45 3.44 1.35l2.58-2.58A9 9 0 0 0 .96 4.95l3.01 2.33C4.68 5.16 6.66 3.58 9 3.58z"/></svg>';

type Lang = 'bg' | 'en';
type View = 'signin' | 'signup' | 'forgot';

const SHOWCASE_IMG = 'https://images.unsplash.com/photo-1570077188670-e3a8d69ac5ff?w=1200&q=80&auto=format&fit=crop';

const ICON_PATHS: Record<string, string> = {
  globe: '<circle cx="12" cy="12" r="10"/><path d="M2 12h20M12 2a15 15 0 0 1 0 20 15 15 0 0 1 0-20Z"/>',
  mail: '<rect x="2" y="4" width="20" height="16" rx="2"/><path d="m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7"/>',
  lock: '<rect x="3" y="11" width="18" height="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/>',
  user: '<path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/>',
  building: '<rect x="4" y="2" width="16" height="20" rx="2"/><path d="M9 22v-4h6v4M9 6h.01M15 6h.01M9 10h.01M15 10h.01M9 14h.01M15 14h.01"/>',
  eye: '<path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7Z"/><circle cx="12" cy="12" r="3"/>',
  eyeOff: '<path d="M9.9 4.2A9.1 9.1 0 0 1 12 4c6.5 0 10 7 10 7a13.2 13.2 0 0 1-2.2 3M6.6 6.6A13.3 13.3 0 0 0 2 11s3.5 7 10 7a9 9 0 0 0 4.5-1.2"/><path d="M9.9 9.9a3 3 0 0 0 4.2 4.2M2 2l20 20"/>',
  arrowR: '<path d="M5 12h14M12 5l7 7-7 7"/>',
  chevL: '<path d="m15 18-6-6 6-6"/>',
  shield: '<path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>',
  package: '<path d="m7.5 4.3 9 5.2M3.3 7 12 12l8.7-5M12 22V12M21 16V8a2 2 0 0 0-1-1.7l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.7l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z"/>',
  layers: '<path d="m12 2 9 5-9 5-9-5 9-5Z"/><path d="m3 12 9 5 9-5M3 17l9 5 9-5"/>',
};

const AUTH_T = {
  bg: {
    langName: 'EN',
    showcase: {
      eyebrow: 'Платформа за дистрибуция',
      quote: 'Публикуваме веднъж и офертите се появяват на всичките ни сайтове, с нашия бранд.',
      author: 'Мария Добрева', role: 'Управител, Слънчев Тур',
      stats: [['120+', 'агенции и оператори'], ['3 200+', 'активни оферти'], ['6', 'канала за вграждане']] as [string, string][],
    },
    signin: {
      title: 'Добре дошли отново', sub: 'Влезте в работното си пространство в Odisea.',
      email: 'Служебен имейл', pass: 'Парола', forgot: 'Забравена парола?',
      remember: 'Запомни ме', submit: 'Вход', or: 'или',
      google: 'Продължете с Google', noAcc: 'Нямате акаунт?', signup: 'Създайте акаунт',
    },
    signup: {
      title: 'Създайте акаунт', sub: 'Започнете да дистрибутирате каталога си за минути.',
      name: 'Име и фамилия', agency: 'Име на агенция / оператор', email: 'Служебен имейл',
      pass: 'Парола', passHint: 'Поне 8 символа, с цифра и главна буква',
      role: 'Вие сте', roles: [['agency', 'Агенция'], ['operator', 'Туроператор'], ['both', 'И двете']] as [string, string][],
      terms: 'Съгласявам се с Условията и Политиката за поверителност',
      submit: 'Създайте акаунт', google: 'Регистрация с Google',
      hasAcc: 'Вече имате акаунт?', signin: 'Вход',
    },
    forgot: {
      title: 'Възстановяване на парола', sub: 'Въведете имейла си и ще ви изпратим връзка за нулиране.',
      email: 'Служебен имейл', submit: 'Изпратете връзка', back: 'Назад към вход',
      sentTitle: 'Проверете пощата си', sentSub: 'Изпратихме връзка за нулиране на',
    },
    errors: { signin: 'Невалиден имейл или парола.', signup: 'Регистрацията не бе успешна.' },
    legal: 'Защитено · GDPR · WCAG AA',
  },
  en: {
    langName: 'BG',
    showcase: {
      eyebrow: 'Distribution platform',
      quote: 'We publish once and offers show up across all our sites, in our brand.',
      author: 'Maria Dobreva', role: 'Manager, Slantsev Tur',
      stats: [['120+', 'agencies & operators'], ['3,200+', 'active offers'], ['6', 'embed channels']] as [string, string][],
    },
    signin: {
      title: 'Welcome back', sub: 'Sign in to your Odisea workspace.',
      email: 'Work email', pass: 'Password', forgot: 'Forgot password?',
      remember: 'Remember me', submit: 'Sign in', or: 'or',
      google: 'Continue with Google', noAcc: 'No account yet?', signup: 'Create one',
    },
    signup: {
      title: 'Create your account', sub: 'Start distributing your catalog in minutes.',
      name: 'Full name', agency: 'Agency / operator name', email: 'Work email',
      pass: 'Password', passHint: 'At least 8 characters, with a number and a capital',
      role: 'You are', roles: [['agency', 'Agency'], ['operator', 'Tour operator'], ['both', 'Both']] as [string, string][],
      terms: 'I agree to the Terms and Privacy Policy',
      submit: 'Create account', google: 'Sign up with Google',
      hasAcc: 'Already have an account?', signin: 'Sign in',
    },
    forgot: {
      title: 'Reset your password', sub: "Enter your email and we'll send you a reset link.",
      email: 'Work email', submit: 'Send reset link', back: 'Back to sign in',
      sentTitle: 'Check your inbox', sentSub: 'We sent a reset link to',
    },
    errors: { signin: 'Invalid email or password.', signup: 'Registration failed.' },
    legal: 'Secure · GDPR · WCAG AA',
  },
};

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [FormsModule, RouterLink, NgTemplateOutlet],
  templateUrl: './login.page.html',
  styleUrl: './login.page.css',
})
export class LoginPage {
  private auth = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private sanitizer = inject(DomSanitizer);
  private svgCache = new Map<string, SafeHtml>();

  showcaseImg = SHOWCASE_IMG;
  googleSvg = this.sanitizer.bypassSecurityTrustHtml(GOOGLE_SVG);
  roleIcons: Record<string, string> = { agency: 'globe', operator: 'package', both: 'layers' };

  lang = signal<Lang>(this.initialLang());
  view = signal<View>(this.route.snapshot.data['view'] === 'signup' ? 'signup' : 'signin');
  t = computed(() => AUTH_T[this.lang()]);

  loading = signal(false);
  error = signal<string | null>(null);
  showPass = signal(false);
  sent = signal(false);

  // Form state.
  email = '';
  password = '';
  remember = true;
  name = '';
  agency = '';
  role = signal('agency');
  agree = signal(false);
  forgotEmail = '';

  private initialLang(): Lang {
    try {
      const v = localStorage.getItem('odisea_lang');
      if (v === 'bg' || v === 'en') return v;
    } catch { /* ignore */ }
    return 'bg';
  }

  toggleLang(): void {
    const next: Lang = this.lang() === 'bg' ? 'en' : 'bg';
    this.lang.set(next);
    try { localStorage.setItem('odisea_lang', next); } catch { /* ignore */ }
    document.documentElement.lang = next;
  }

  go(view: View): void {
    this.error.set(null);
    this.sent.set(false);
    this.view.set(view);
  }

  submitSignin(): void {
    if (!this.email || !this.password) return;
    this.loading.set(true);
    this.error.set(null);
    this.auth.login({ email: this.email, password: this.password }).subscribe({
      next: () => this.router.navigateByUrl(this.returnUrl()),
      error: () => { this.error.set(this.t().errors.signin); this.loading.set(false); },
    });
  }

  submitSignup(): void {
    if (!this.agree() || !this.name || !this.email || !this.password) return;
    this.loading.set(true);
    this.error.set(null);
    this.auth.register({
      email: this.email,
      password: this.password,
      displayName: this.name,
      tenantName: this.agency || undefined,
      tenantRole: this.agency ? this.role() : undefined,
    }).subscribe({
      next: () => this.router.navigateByUrl('/offers'),
      error: (e) => { this.error.set(e?.error?.detail ?? this.t().errors.signup); this.loading.set(false); },
    });
  }

  submitForgot(): void {
    // No reset endpoint yet — confirm optimistically (the design's "check your inbox").
    this.sent.set(true);
  }

  private returnUrl(): string {
    return this.route.snapshot.queryParamMap.get('returnUrl') ?? '/offers';
  }

  svg(name: string, size = 17): SafeHtml {
    const key = `${name}:${size}`;
    let v = this.svgCache.get(key);
    if (!v) {
      const markup = `<svg width="${size}" height="${size}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">${ICON_PATHS[name] ?? ''}</svg>`;
      v = this.sanitizer.bypassSecurityTrustHtml(markup);
      this.svgCache.set(key, v);
    }
    return v;
  }
}
