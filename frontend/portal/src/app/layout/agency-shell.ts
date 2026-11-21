import { Component, inject } from '@angular/core';
import { EntitlementsService } from '../core/entitlements.service';
import { t } from '../core/i18n/i18n';
import { Shell } from './shell';
import { NavAction, NavGroup, NavItem } from './side-nav';

@Component({
  selector: 'app-agency-shell',
  imports: [Shell],
  template: `<app-shell [area]="area" [groups]="groups" [primaryAction]="newBooking" [tabs]="tabs" />`,
})
export class AgencyShell {
  readonly area = t('nav.agencyArea');
  readonly newBooking: NavAction = { label: t('nav.newBooking'), link: '/agency/search', icon: 'plus' };

  readonly tabs: NavItem[] = [
    { label: t('nav.search'), link: '/agency/search', icon: 'search' },
    { label: t('nav.programs'), link: '/agency/programs', icon: 'layers' },
    { label: t('nav.bookings'), link: '/agency/bookings', icon: 'bookings' },
    { label: t('nav.documentsShort'), link: '/agency/documents', icon: 'documents' },
  ];

  readonly groups: NavGroup[] = [
    {
      items: [
        { label: t('nav.search'), link: '/agency/search', icon: 'search' },
        { label: t('nav.programs'), link: '/agency/programs', icon: 'layers' },
        { label: t('nav.bookings'), link: '/agency/bookings', icon: 'bookings' },
        { label: t('nav.documents'), link: '/agency/documents', icon: 'documents' },
      ],
    },
  ];

  constructor() {
    // Plan-gated navigation reads this; the result is cached for the session.
    void inject(EntitlementsService).load();
  }
}
