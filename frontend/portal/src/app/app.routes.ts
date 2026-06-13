import { Routes } from '@angular/router';
import { OffersPage } from './offers.page';
import { CollectionsPage } from './collections.page';
import { authGuard } from './auth/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'offers' },
  {
    path: 'login',
    title: 'Sign in — Odisea',
    loadComponent: () => import('./auth/login.page').then(m => m.LoginPage),
  },
  { path: 'offers', component: OffersPage, title: 'Offers — Odisea' },
  { path: 'collections', component: CollectionsPage, title: 'Collections — Odisea' },
  {
    path: 'builder',
    title: 'Publication composer — Odisea',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./builder.page').then(m => m.BuilderPage),
  },
  {
    path: 'themes/:id',
    title: 'Theme studio — Odisea',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./theme-studio.page').then(m => m.ThemeStudioPage),
  },
  { path: '**', redirectTo: 'offers' },
];
