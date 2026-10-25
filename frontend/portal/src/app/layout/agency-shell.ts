import { Component } from '@angular/core';
import { Shell } from './shell';
import { NavAction, NavGroup } from './side-nav';

@Component({
  selector: 'app-agency-shell',
  imports: [Shell],
  template: `<app-shell area="Partner portal" [groups]="groups" [primaryAction]="newBooking" />`,
})
export class AgencyShell {
  readonly newBooking: NavAction = { label: 'New booking', link: '/agency/search', icon: 'plus' };

  readonly groups: NavGroup[] = [
    {
      items: [
        { label: 'Search', link: '/agency/search', icon: 'search' },
        { label: 'Bookings', link: '/agency/bookings', icon: 'bookings' },
        { label: 'Documents', link: '/agency/documents', icon: 'documents' },
      ],
    },
  ];
}
