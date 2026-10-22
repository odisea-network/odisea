import { Routes } from '@angular/router';
import { adminGuard, agencyGuard } from './core/guards';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login').then(m => m.Login),
  },
  {
    path: 'agency',
    canActivate: [agencyGuard],
    loadComponent: () => import('./layout/agency-shell').then(m => m.AgencyShell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'programs' },
      {
        path: 'programs',
        loadComponent: () => import('./features/agency/programs').then(m => m.AgencyPrograms),
      },
      {
        path: 'bookings',
        loadComponent: () => import('./features/placeholder').then(m => m.Placeholder),
        data: { title: 'Bookings' },
      },
      {
        path: 'documents',
        loadComponent: () => import('./features/placeholder').then(m => m.Placeholder),
        data: { title: 'Documents' },
      },
    ],
  },
  {
    path: 'admin',
    canActivate: [adminGuard],
    loadComponent: () => import('./layout/admin-shell').then(m => m.AdminShell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'agencies' },
      ...['agencies', 'programs', 'pricing', 'bookings', 'documents'].map(section => ({
        path: section,
        loadComponent: () => import('./features/placeholder').then(m => m.Placeholder),
        data: { title: section[0].toUpperCase() + section.slice(1) },
      })),
    ],
  },
  { path: '**', redirectTo: 'login' },
];
