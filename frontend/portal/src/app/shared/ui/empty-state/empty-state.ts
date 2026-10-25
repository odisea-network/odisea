import { Component, input } from '@angular/core';
import { Icon } from '../icon/icon';
import { IconName } from '../icon/icons';

/** Empty, error, forbidden and offline states share one shape: icon, a line, a way out. */
@Component({
  selector: 'ui-empty-state',
  imports: [Icon],
  template: `
    <span class="empty__icon"><ui-icon [name]="icon()" size="lg" /></span>
    <b class="empty__title">{{ title() }}</b>
    @if (description(); as text) {
      <span class="empty__text">{{ text }}</span>
    }
    <div class="empty__actions"><ng-content /></div>
  `,
  styleUrl: './empty-state.scss',
  host: { '[attr.role]': 'tone() === "error" ? "alert" : null', '[class]': '"empty empty--" + tone()' },
})
export class EmptyState {
  readonly icon = input<IconName>('clock');
  readonly title = input.required<string>();
  readonly description = input<string | null>(null);
  readonly tone = input<'neutral' | 'error'>('neutral');
}
