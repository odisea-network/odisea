import { Component, computed, inject } from '@angular/core';
import { EntitlementsService } from '../core/entitlements.service';
import { t } from '../core/i18n/i18n';
import { Shell } from './shell';
import { NavAction, NavGroup, NavItem } from './side-nav';

@Component({
  selector: 'app-agency-shell',
  imports: [Shell],
  template: `<app-shell [area]="area" [groups]="groups()" [primaryAction]="newBooking" [tabs]="tabs()" />`,
})
export class AgencyShell {
  readonly area = t('nav.agencyArea');
  readonly newBooking: NavAction = { label: t('nav.newBooking'), link: '/agency/search', icon: 'plus' };

  private readonly baseTabs: NavItem[] = [
    { label: t('nav.search'), link: '/agency/search', icon: 'search' },
    { label: t('nav.programs'), link: '/agency/programs', icon: 'layers' },
    { label: t('nav.bookings'), link: '/agency/bookings', icon: 'bookings' },
    { label: t('nav.documentsShort'), link: '/agency/documents', icon: 'documents' },
  ];

  private readonly entitlements = inject(EntitlementsService);

  private readonly main: NavGroup = {
    items: [
      { label: t('nav.search'), link: '/agency/search', icon: 'search' },
      { label: t('nav.programs'), link: '/agency/programs', icon: 'layers' },
      { label: t('nav.bookings'), link: '/agency/bookings', icon: 'bookings' },
      { label: t('nav.documents'), link: '/agency/documents', icon: 'documents' },
    ],
  };

  private readonly ledger: NavGroup = {
    label: t('ledger.nav.group'),
    items: [
      { label: t('ledger.nav.overview'), link: '/agency/ledger', icon: 'home', exact: true },
      { label: t('ledger.nav.reservations'), link: '/agency/ledger/reservations', icon: 'card' },
      { label: t('ledger.nav.operators'), link: '/agency/ledger/operators', icon: 'building' },
    ],
  };

  /** The ledger appears only for agencies whose plan includes it. */
  readonly groups = computed<NavGroup[]>(() =>
    this.entitlements.has('ReservationLedger') ? [this.main, this.ledger] : [this.main]);

  readonly tabs = computed<NavItem[]>(() => this.entitlements.has('ReservationLedger')
    ? [...this.baseTabs, { label: t('ledger.nav.tab'), link: '/agency/ledger', icon: 'card' }]
    : this.baseTabs);

  constructor() {
    void this.entitlements.load();
  }
}
