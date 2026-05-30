import { Routes } from '@angular/router';
import { OffersPage } from './offers.page';
import { CollectionsPage } from './collections.page';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'offers' },
  { path: 'offers', component: OffersPage, title: 'Offers — Odisea' },
  { path: 'collections', component: CollectionsPage, title: 'Collections — Odisea' },
  {
    path: 'builder',
    title: 'Collection builder — Odisea',
    loadComponent: () =>
      import('./placeholder.page').then(m => m.PlaceholderPage),
  },
  { path: '**', redirectTo: 'offers' },
];
