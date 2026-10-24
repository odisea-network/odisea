import { Component, inject, input } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { BrandingService } from '../core/branding.service';

export interface NavLink {
  path: string;
  label: string;
}

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="shell">
      <header>
        <span class="brand">
          @if (brand.logoSrc(); as logo) {
            <img [src]="logo" alt="" />
          }
          {{ brand.branding().displayName }}
        </span>
        <nav>
          @for (link of links(); track link.path) {
            <a [routerLink]="link.path" routerLinkActive="active">{{ link.label }}</a>
          }
        </nav>
        <div class="user">
          <span>{{ auth.user()?.fullName }}</span>
          <button type="button" class="ghost" (click)="logout()">Sign out</button>
        </div>
      </header>
      <main>
        <router-outlet />
      </main>
      @if (brand.branding().legalFooter || brand.branding().supportEmail || brand.branding().supportPhone) {
        <footer>
          <span>{{ brand.branding().legalFooter }}</span>
          <span class="contact">
            @if (brand.branding().supportEmail; as email) {
              <a [href]="'mailto:' + email">{{ email }}</a>
            }
            @if (brand.branding().supportPhone; as phone) {
              <span>{{ phone }}</span>
            }
          </span>
        </footer>
      }
    </div>
  `,
  styles: `
    .shell { min-height: 100dvh; display: flex; flex-direction: column; }
    header {
      display: flex; align-items: center; gap: 2rem; padding: 0 1.5rem; height: 3.5rem;
      background: var(--surface-1); border-bottom: 1px solid var(--border);
    }
    .brand { display: flex; align-items: center; gap: .6rem; font-weight: 700; font-size: 1.1rem; color: var(--accent-2); }
    .brand img { height: 2rem; max-width: 9rem; object-fit: contain; }
    nav { display: flex; gap: 1rem; flex: 1; }
    nav a {
      padding: .4rem .7rem; border-radius: .4rem; text-decoration: none;
      color: var(--text-muted); font-size: .9rem;
    }
    nav a.active { background: var(--surface-2); color: var(--text); font-weight: 600; }
    .user { display: flex; align-items: center; gap: .75rem; font-size: .85rem; }
    main { flex: 1; padding: 1.5rem; background: var(--surface-2); }
    footer {
      display: flex; justify-content: space-between; gap: 1rem; flex-wrap: wrap;
      padding: .75rem 1.5rem; font-size: .8rem; color: var(--text-muted);
      background: var(--surface-1); border-top: 1px solid var(--border);
    }
    .contact { display: flex; gap: 1rem; }
    footer a { color: inherit; }
  `,
})
export class Shell {
  protected readonly auth = inject(AuthService);
  protected readonly brand = inject(BrandingService);
  private readonly router = inject(Router);

  readonly links = input.required<NavLink[]>();

  logout(): void {
    this.auth.logout();
    void this.router.navigate(['/login']);
  }
}
