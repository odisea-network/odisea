import { Component } from '@angular/core';
import { Shell } from './shell';
import { NavGroup } from './side-nav';

@Component({
  selector: 'app-admin-shell',
  imports: [Shell],
  template: `<app-shell area="Back-office" [groups]="groups" />`,
})
export class AdminShell {
  readonly groups: NavGroup[] = [
    { items: [{ label: 'Bookings', link: '/admin/bookings', icon: 'bookings' }] },
    { label: 'Partners', items: [{ label: 'Agencies', link: '/admin/agencies', icon: 'building' }] },
    { label: 'Catalogue', items: [{ label: 'Programs', link: '/admin/programs', icon: 'layers' }] },
    { label: 'Pricing', items: [{ label: 'Pricing rules', link: '/admin/pricing', icon: 'percent' }] },
    { label: 'Finance', items: [{ label: 'Documents', link: '/admin/documents', icon: 'documents' }] },
  ];
}
