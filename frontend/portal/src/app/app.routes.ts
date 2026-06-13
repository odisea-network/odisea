import { Routes } from '@angular/router';
import { OffersPage } from './offers.page';
import { CollectionsPage } from './collections.page';
import { authGuard } from './auth/auth.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    title: 'Odisea — Дистрибуция на туристически продукти',
    loadComponent: () => import('./landing.page').then(m => m.LandingPage),
  },
  {
    path: 'login',
    title: 'Вход — Odisea',
    data: { view: 'signin' },
    loadComponent: () => import('./auth/login.page').then(m => m.LoginPage),
  },
  {
    path: 'register',
    title: 'Регистрация — Odisea',
    data: { view: 'signup' },
    loadComponent: () => import('./auth/login.page').then(m => m.LoginPage),
  },
  { path: 'offers', component: OffersPage, title: 'Offers — Odisea' },
  {
    path: 'collections',
    component: CollectionsPage,
    title: 'Collections — Odisea',
    canActivate: [authGuard],
  },
  {
    path: 'operator/offers',
    title: 'My offers — Odisea',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./operator-offers.page').then(m => m.OperatorOffersPage),
  },
  {
    path: 'operator/connections',
    title: 'Connections — Odisea',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./connections.page').then(m => m.ConnectionsPage),
  },
  {
    path: 'leads',
    title: 'Leads — Odisea',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./leads.page').then(m => m.LeadsPage),
  },
  {
    path: 'marketplace',
    title: 'Theme marketplace — Odisea',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./theme-marketplace.page').then(m => m.ThemeMarketplacePage),
  },
  {
    path: 'webhooks',
    title: 'Webhooks — Odisea',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./webhooks.page').then(m => m.WebhooksPage),
  },
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
