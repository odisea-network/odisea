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
      { path: '', pathMatch: 'full', redirectTo: 'search' },
      {
        path: 'search',
        loadComponent: () => import('./features/agency/search').then(m => m.AgencySearch),
      },
      {
        path: 'book',
        loadComponent: () => import('./features/agency/book').then(m => m.AgencyBook),
      },
      {
        path: 'bookings',
        loadComponent: () => import('./features/agency/bookings').then(m => m.AgencyBookings),
      },
      {
        path: 'bookings/:id',
        loadComponent: () => import('./features/agency/booking-detail').then(m => m.AgencyBookingDetail),
      },
      {
        path: 'documents',
        loadComponent: () => import('./features/agency/documents').then(m => m.AgencyDocuments),
      },
    ],
  },
  {
    path: 'admin',
    canActivate: [adminGuard],
    loadComponent: () => import('./layout/admin-shell').then(m => m.AdminShell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'bookings' },
      {
        path: 'bookings',
        loadComponent: () => import('./features/admin/bookings').then(m => m.AdminBookings),
      },
      {
        path: 'bookings/:id',
        loadComponent: () => import('./features/admin/booking-detail').then(m => m.AdminBookingDetail),
      },
      ...['agencies', 'programs', 'pricing', 'documents'].map(section => ({
        path: section,
        loadComponent: () => import('./features/placeholder').then(m => m.Placeholder),
        data: { title: section[0].toUpperCase() + section.slice(1) },
      })),
    ],
  },
  { path: '**', redirectTo: 'login' },
];
