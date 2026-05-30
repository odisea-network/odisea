import { Component } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';

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
      </nav>
    </header>
    <main>
      <router-outlet />
    </main>
  `,
  styles: [`
    :host { display: block; font-family: system-ui, sans-serif; color: #1a1a1a; }
    header {
      display: flex; align-items: center; justify-content: space-between;
      padding: 14px 28px; background: #0a2540; color: #fff;
    }
    .brand { font-weight: 700; letter-spacing: 0.02em; }
    nav a {
      color: #cfd9e6; margin-left: 18px; text-decoration: none; font-size: 0.95rem;
    }
    nav a.active, nav a:hover { color: #fff; }
    main { padding: 24px 28px; max-width: 1200px; margin: 0 auto; }
    h2 { margin-top: 0; }
  `],
})
export class App {}
