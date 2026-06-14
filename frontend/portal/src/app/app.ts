import { Component, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from './auth/auth.service';
import { OdIcon } from './shared/od-icon';

interface NavItem { label: string; icon: string; link: string; exact?: boolean; }
interface NavGroup { group: string | null; items: NavItem[]; }

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, OdIcon],
  template: `
    @if (showShell()) {
      <div class="shell">
        <aside class="side">
          <a routerLink="/" class="brand">
            <svg width="22" height="26" viewBox="0 0 32 38" fill="none" aria-hidden="true">
              <path d="M16 37V12" stroke="var(--od-gold-500)" stroke-width="1.6" stroke-linecap="round" />
              <path d="M16 13c-1.8-3.2-5.6-4.4-9.2-3.8 0 3.8 2.6 7 6.4 7.6 1.4.2 2.2 0 2.8-.4" fill="var(--od-gold-500)" opacity="0.95" />
              <path d="M16 13c1.8-3.2 5.6-4.4 9.2-3.8 0 3.8-2.6 7-6.4 7.6-1.4.2-2.2 0-2.8-.4" fill="var(--od-gold-500)" opacity="0.95" />
              <path d="M16 22c-1.6-2.8-4.8-3.8-8-3.3 0 3.3 2.3 6 5.6 6.6 1.2.2 1.9 0 2.4-.3" fill="var(--od-gold-500)" opacity="0.78" />
              <path d="M16 22c1.6-2.8 4.8-3.8 8-3.3 0 3.3-2.3 6-5.6 6.6-1.2.2-1.9 0-2.4-.3" fill="var(--od-gold-500)" opacity="0.78" />
              <path d="M16 4.5c-1.7 1.8-1.7 4.7 0 6.8 1.7-2.1 1.7-5 0-6.8Z" fill="var(--od-gold-500)" />
            </svg>
            <span class="brand__text">odisea</span>
          </a>

          @if (auth.isAuthenticated()) {
            <div class="tenant">
              <div class="tenant__avatar">{{ initials() }}</div>
              <div class="tenant__meta">
                <div class="tenant__name">{{ auth.user()?.displayName }}</div>
                <div class="tenant__role">{{ tenantLine() }}</div>
              </div>
            </div>
          }

          <nav class="nav">
            @for (g of navGroups(); track g.group) {
              <div class="nav__group">
                @if (g.group) { <div class="od-eyebrow nav__grouplabel">{{ g.group }}</div> }
                @for (it of g.items; track it.link) {
                  <a class="nav__link" [routerLink]="it.link" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: !!it.exact }">
                    <span class="nav__bar"></span><od-icon [name]="it.icon" [size]="18" />{{ it.label }}
                  </a>
                }
              </div>
            }
          </nav>

          <div class="side__foot">
            @if (auth.isAuthenticated()) {
              <button type="button" class="nav__link" (click)="logout()"><span class="nav__bar"></span><od-icon name="logout" [size]="18" />Изход</button>
              <div class="me">
                <div class="me__avatar">{{ initials() }}</div>
                <div class="tenant__meta">
                  <div class="tenant__name">{{ auth.user()?.displayName }}</div>
                  <div class="tenant__role">{{ auth.role() }}</div>
                </div>
              </div>
            } @else {
              <a routerLink="/login" class="od-btn od-btn--primary od-btn--full">Вход</a>
            }
          </div>
        </aside>

        <div class="content">
          <header class="topbar">
            <div class="searchbox"><od-icon name="search" [size]="16" /><span>Търси оферти, колекции…</span></div>
            <div class="topbar__right">
              <button type="button" class="od-iconbtn" title="Известия"><od-icon name="bell" [size]="18" /></button>
              <button type="button" class="od-iconbtn" title="Помощ"><od-icon name="info" [size]="18" /></button>
              @if (auth.isAuthenticated()) {
                <div class="topbar__divider"></div>
                <div class="topbar__avatar" [title]="auth.user()?.displayName || ''">{{ initials() }}</div>
              }
            </div>
          </header>
          <main class="main"><router-outlet /></main>
        </div>
      </div>
    } @else {
      <router-outlet />
    }
  `,
  styles: [`
    :host { display: block; }
    .shell { min-height: 100vh; display: flex; background: var(--od-bg); }
    .side { width: 240px; flex: none; border-right: 1px solid var(--od-border); background: var(--od-surface); display: flex; flex-direction: column; position: sticky; top: 0; height: 100vh; }
    .brand { display: inline-flex; align-items: flex-end; gap: 8px; text-decoration: none; padding: 18px 16px 12px; }
    .brand__text { font-family: var(--od-serif); font-weight: 600; font-size: 21px; letter-spacing: 0.01em; color: var(--od-teal-800); line-height: 0.9; }
    .tenant { display: flex; align-items: center; gap: 10px; margin: 0 12px 6px; padding: 8px 10px; border-radius: var(--od-r-md); background: var(--od-100); border: 1px solid var(--od-border-2); }
    .tenant__avatar { width: 30px; height: 30px; border-radius: 8px; background: var(--od-teal-800); color: #fff; display: grid; place-items: center; font-weight: 700; font-size: 13px; flex: none; }
    .tenant__meta { min-width: 0; line-height: 1.25; }
    .tenant__name { font-size: 13px; font-weight: 600; color: var(--od-ink); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .tenant__role { font-size: 11px; color: var(--od-500); }
    .nav { display: flex; flex-direction: column; gap: 2px; padding: 10px 12px; flex: 1; min-height: 0; overflow-y: auto; }
    .nav__group { margin-bottom: 6px; }
    .nav__grouplabel { padding: 10px 11px 6px; }
    .nav__link { display: flex; align-items: center; gap: 11px; width: 100%; height: 40px; padding: 0 11px; border: 0; border-radius: var(--od-r-md); cursor: pointer; text-align: left; position: relative; font-family: var(--od-font); font-size: 14px; font-weight: 500; background: transparent; color: var(--od-700); text-decoration: none; transition: background var(--od-fast), color var(--od-fast); }
    .nav__link:hover { background: var(--od-100); }
    .nav__link.active { background: var(--od-teal-50); color: var(--od-teal-700); font-weight: 600; }
    .nav__bar { position: absolute; left: -11px; top: 9px; bottom: 9px; width: 3px; border-radius: 3px; background: transparent; }
    .nav__link.active .nav__bar { background: var(--od-gold-500); }
    .side__foot { margin-top: auto; padding: 12px; display: flex; flex-direction: column; gap: 2px; }
    .me { display: flex; align-items: center; gap: 10px; padding: 10px 11px; margin-top: 4px; border-top: 1px solid var(--od-border-2); }
    .me__avatar { width: 32px; height: 32px; border-radius: 50%; background: linear-gradient(135deg, var(--od-teal-500), var(--od-teal-800)); color: #fff; display: grid; place-items: center; font-size: 12px; font-weight: 700; flex: none; }
    .content { flex: 1; min-width: 0; display: flex; flex-direction: column; }
    .topbar { height: 60px; border-bottom: 1px solid var(--od-border); background: color-mix(in srgb, var(--od-surface) 88%, transparent); backdrop-filter: blur(8px); display: flex; align-items: center; padding: 0 24px; position: sticky; top: 0; z-index: 20; gap: 12px; }
    .searchbox { display: flex; align-items: center; gap: 8px; height: 36px; padding: 0 12px; min-width: 240px; border: 1px solid var(--od-border); border-radius: var(--od-r-md); background: var(--od-surface); color: var(--od-500); font-size: 13px; }
    .topbar__right { margin-left: auto; display: flex; align-items: center; gap: 6px; }
    .topbar__divider { width: 1px; height: 22px; background: var(--od-border); margin: 0 4px; }
    .topbar__avatar { width: 34px; height: 34px; border-radius: 50%; background: linear-gradient(135deg, var(--od-teal-500), var(--od-teal-800)); color: #fff; display: grid; place-items: center; font-size: 13px; font-weight: 700; }
    .main { flex: 1; min-width: 0; }
    @media (max-width: 860px) {
      .side { display: none; }
      .searchbox { display: none; }
    }
  `],
})
export class App {
  auth = inject(AuthService);
  private router = inject(Router);

  private static readonly BARE_ROUTES = ['/', '', '/login', '/register'];
  showShell = signal(!App.isBare(this.router.url));

  navGroups = computed<NavGroup[]>(() => {
    const groups: NavGroup[] = [
      { group: null, items: [{ label: 'Оферти', icon: 'package', link: '/offers', exact: true }] },
    ];
    if (this.auth.isOperator()) {
      groups.push({ group: 'Каталог', items: [
        { label: 'Моите оферти', icon: 'package', link: '/operator/offers' },
        { label: 'Връзки', icon: 'link', link: '/operator/connections' },
      ] });
    }
    if (this.auth.isAgency()) {
      groups.push({ group: 'Дистрибуция', items: [
        { label: 'Колекции', icon: 'layers', link: '/collections' },
        { label: 'Композитор', icon: 'grid', link: '/builder' },
        { label: 'Теми', icon: 'sliders', link: '/marketplace' },
        { label: 'Известия', icon: 'send', link: '/webhooks' },
      ] });
      groups.push({ group: 'Резултати', items: [
        { label: 'Запитвания', icon: 'users', link: '/leads' },
      ] });
    }
    return groups;
  });

  constructor() {
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe(e => this.showShell.set(!App.isBare(e.urlAfterRedirects)));
  }

  private static isBare(url: string): boolean {
    const path = url.split(/[?#]/)[0];
    return App.BARE_ROUTES.includes(path);
  }

  initials(): string {
    const name = this.auth.user()?.displayName ?? '';
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) return '·';
    return (parts[0][0] + (parts[1]?.[0] ?? '')).toUpperCase();
  }

  tenantLine(): string {
    const u = this.auth.user();
    if (!u) return '';
    const tenant = u.tenantType === 'Operator' ? 'Туроператор' : u.tenantType === 'Agency' ? 'Агенция' : 'Платформа';
    return tenant;
  }

  logout(): void {
    this.auth.logout();
    this.router.navigateByUrl('/login');
  }
}
