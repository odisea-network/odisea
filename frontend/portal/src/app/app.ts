import { Component, inject } from '@angular/core';
import { Router, RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from './auth/auth.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <header>
      <div class="brand">Odisea Network</div>
      <nav>
        <a routerLink="/offers" routerLinkActive="active">Offers</a>
        <a routerLink="/collections" routerLinkActive="active">Collections</a>
        <a routerLink="/builder" routerLinkActive="active">Builder</a>
        @if (auth.isOperator()) {
          <a routerLink="/operator/offers" routerLinkActive="active">My offers</a>
        }
        @if (auth.isAgency()) {
          <a routerLink="/leads" routerLinkActive="active">Leads</a>
        }
      </nav>
      <div class="account">
        @if (auth.isAuthenticated()) {
          <span class="who">{{ auth.user()?.displayName }} · {{ auth.role() }}</span>
          <button type="button" (click)="logout()">Sign out</button>
        } @else {
          <a routerLink="/login" class="signin">Sign in</a>
        }
      </div>
    </header>
    <main>
      <router-outlet />
    </main>
  `,
  styles: [`
    :host { display: block; font-family: system-ui, sans-serif; color: #1a1a1a; }
    header {
      display: flex; align-items: center; gap: 24px;
      padding: 14px 28px; background: #0a2540; color: #fff;
    }
    .brand { font-weight: 700; letter-spacing: 0.02em; }
    nav { flex: 1; }
    nav a {
      color: #cfd9e6; margin-right: 18px; text-decoration: none; font-size: 0.95rem;
    }
    nav a.active, nav a:hover { color: #fff; }
    .account { display: flex; align-items: center; gap: 12px; }
    .who { color: #cfd9e6; font-size: 0.85rem; }
    .signin { color: #fff; text-decoration: none; font-size: 0.9rem; }
    .account button {
      background: transparent; border: 1px solid #335; color: #cfd9e6;
      padding: 5px 10px; border-radius: 6px; cursor: pointer; font-size: 0.85rem;
    }
    .account button:hover { color: #fff; border-color: #557; }
    main { padding: 24px 28px; max-width: 1200px; margin: 0 auto; }
    h2 { margin-top: 0; }
  `],
})
export class App {
  auth = inject(AuthService);
  private router = inject(Router);

  logout(): void {
    this.auth.logout();
    this.router.navigateByUrl('/login');
  }
}
