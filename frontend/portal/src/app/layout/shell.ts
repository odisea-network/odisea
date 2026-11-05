import { Component, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs/operators';
import { AuthService } from '../core/auth.service';
import { BrandingService } from '../core/branding.service';
import { ButtonDirective, Icon } from '../shared/ui';
import { NavAction, NavGroup, NavItem, SideNav } from './side-nav';

/**
 * Page frame for both areas: sticky sidebar on wide screens, a top bar with a
 * drawer below 900px, the routed page, and the operator's legal footer.
 */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, SideNav, ButtonDirective, Icon],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  protected readonly auth = inject(AuthService);
  protected readonly brand = inject(BrandingService);
  private readonly router = inject(Router);

  readonly area = input.required<string>();
  readonly groups = input.required<NavGroup[]>();
  readonly primaryAction = input<NavAction | null>(null);
  /** Bottom tab bar on phones; empty means none (operator area). */
  readonly tabs = input<NavItem[]>([]);

  protected readonly drawerOpen = signal(false);

  constructor() {
    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd), takeUntilDestroyed())
      .subscribe(() => this.drawerOpen.set(false));
  }

  protected logout(): void {
    this.auth.logout();
    void this.router.navigate(['/login']);
  }
}
