import { Component } from '@angular/core';
import { NavLink, Shell } from './shell';

@Component({
  selector: 'app-admin-shell',
  imports: [Shell],
  template: `<app-shell [links]="links" />`,
})
export class AdminShell {
  readonly links: NavLink[] = [
    { path: '/admin/agencies', label: 'Agencies' },
    { path: '/admin/programs', label: 'Programs' },
    { path: '/admin/pricing', label: 'Pricing' },
    { path: '/admin/bookings', label: 'Bookings' },
    { path: '/admin/documents', label: 'Documents' },
  ];
}
