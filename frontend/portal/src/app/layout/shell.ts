import { Component, inject, input } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';

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
        <span class="brand">Odisea</span>
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
    </div>
  `,
  styles: `
    .shell { min-height: 100dvh; display: flex; flex-direction: column; }
    header {
      display: flex; align-items: center; gap: 2rem; padding: 0 1.5rem; height: 3.5rem;
      background: var(--surface-1); border-bottom: 1px solid var(--border);
    }
    .brand { font-weight: 700; font-size: 1.1rem; }
    nav { display: flex; gap: 1rem; flex: 1; }
    nav a {
      padding: .4rem .7rem; border-radius: .4rem; text-decoration: none;
      color: var(--text-muted); font-size: .9rem;
    }
    nav a.active { background: var(--surface-2); color: var(--text); font-weight: 600; }
    .user { display: flex; align-items: center; gap: .75rem; font-size: .85rem; }
    main { flex: 1; padding: 1.5rem; background: var(--surface-2); }
  `,
})
export class Shell {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly links = input.required<NavLink[]>();

  logout(): void {
    this.auth.logout();
    void this.router.navigate(['/login']);
  }
}
