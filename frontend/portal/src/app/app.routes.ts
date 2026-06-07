import { Routes } from '@angular/router';
import { OffersPage } from './offers.page';
import { CollectionsPage } from './collections.page';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'offers' },
  { path: 'offers', component: OffersPage, title: 'Offers — Odisea' },
  { path: 'collections', component: CollectionsPage, title: 'Collections — Odisea' },
  {
    path: 'builder',
    title: 'Publication composer — Odisea',
    loadComponent: () =>
      import('./builder.page').then(m => m.BuilderPage),
  },
  {
    path: 'themes/:id',
    title: 'Theme studio — Odisea',
    loadComponent: () =>
      import('./theme-studio.page').then(m => m.ThemeStudioPage),
  },
  { path: '**', redirectTo: 'offers' },
];
