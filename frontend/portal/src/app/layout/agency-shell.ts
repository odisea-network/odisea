import { Component } from '@angular/core';
import { NavLink, Shell } from './shell';

@Component({
  selector: 'app-agency-shell',
  imports: [Shell],
  template: `<app-shell [links]="links" />`,
})
export class AgencyShell {
  readonly links: NavLink[] = [
    { path: '/agency/search', label: 'Search' },
    { path: '/agency/bookings', label: 'Bookings' },
    { path: '/agency/documents', label: 'Documents' },
  ];
}
