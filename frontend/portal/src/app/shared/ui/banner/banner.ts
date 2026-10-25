import { Component, computed, input } from '@angular/core';
import { Icon } from '../icon/icon';
import { IconName } from '../icon/icons';

export type BannerTone = 'info' | 'ok' | 'warn' | 'bad';

const ICON: Record<BannerTone, IconName> = {
  info: 'info',
  ok: 'check-circle',
  warn: 'alert-triangle',
  bad: 'alert-circle',
};

/** Inline feedback that stays on the page (price drift, server errors, guidance). */
@Component({
  selector: 'ui-banner',
  imports: [Icon],
  template: `<ui-icon [name]="icon()" /><div class="banner__body"><ng-content /></div>`,
  styleUrl: './banner.scss',
  host: {
    '[class]': '"banner banner--" + tone()',
    '[attr.role]': 'tone() === "bad" ? "alert" : "status"',
  },
})
export class Banner {
  readonly tone = input<BannerTone>('info');
  protected readonly icon = computed(() => ICON[this.tone()]);
}
