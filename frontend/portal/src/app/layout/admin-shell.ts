import { Component } from '@angular/core';
import { t } from '../core/i18n/i18n';
import { Shell } from './shell';
import { NavGroup } from './side-nav';

@Component({
  selector: 'app-admin-shell',
  imports: [Shell],
  template: `<app-shell [area]="area" [groups]="groups" />`,
})
export class AdminShell {
  readonly area = t('nav.adminArea');

  readonly groups: NavGroup[] = [
    {
      items: [
        { label: t('nav.today'), link: '/admin/today', icon: 'home' },
        { label: t('nav.bookings'), link: '/admin/bookings', icon: 'bookings' },
      ],
    },
    { label: t('nav.partners'), items: [{ label: t('nav.agencies'), link: '/admin/agencies', icon: 'building' }] },
    {
      label: t('nav.catalogue'),
      items: [
        { label: t('nav.programs'), link: '/admin/programs', icon: 'layers' },
        { label: t('nav.hotels'), link: '/admin/hotels', icon: 'hotel' },
        { label: t('nav.destinations'), link: '/admin/destinations', icon: 'map-pin' },
      ],
    },
    { label: t('nav.pricing'), items: [{ label: t('nav.pricingRules'), link: '/admin/pricing', icon: 'percent' }] },
    { label: t('nav.finance'), items: [{ label: t('nav.documents'), link: '/admin/documents', icon: 'documents' }] },
    { label: t('nav.system'), items: [{ label: t('nav.settings'), link: '/admin/settings', icon: 'settings' }] },
  ];
}
