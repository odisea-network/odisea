import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { t } from '../../core/i18n/i18n';
import { TPipe } from '../../shared/pipes/t.pipe';

/** The Settings sections, shown as a segmented link group on every settings page. */
@Component({
  selector: 'app-settings-nav',
  imports: [RouterLink, RouterLinkActive, TPipe],
  template: `
    <a class="seg__link" routerLink="/admin/settings/branding" routerLinkActive="seg__link--on" ariaCurrentWhenActive="page">{{ 'settings.branding' | t }}</a>
    <a class="seg__link" routerLink="/admin/settings/company" routerLinkActive="seg__link--on" ariaCurrentWhenActive="page">{{ 'settings.company' | t }}</a>
  `,
  styleUrl: './settings-nav.scss',
  host: { role: 'navigation', '[attr.aria-label]': 'label' },
})
export class SettingsNav {
  readonly label = t('settings.sections');
}
