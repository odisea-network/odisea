import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

/** The Settings sections, shown as a segmented link group on every settings page. */
@Component({
  selector: 'app-settings-nav',
  imports: [RouterLink, RouterLinkActive],
  template: `
    <a class="seg__link" routerLink="/admin/settings/branding" routerLinkActive="seg__link--on" ariaCurrentWhenActive="page">Branding</a>
    <a class="seg__link" routerLink="/admin/settings/company" routerLinkActive="seg__link--on" ariaCurrentWhenActive="page">Company</a>
  `,
  styleUrl: './settings-nav.scss',
  host: { role: 'navigation', 'aria-label': 'Settings sections' },
})
export class SettingsNav {}
