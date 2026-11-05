import { Component, input } from '@angular/core';
import { Icon } from '../icon/icon';
import { IconName } from '../icon/icons';

export type NextStepTone = 'neutral' | 'ok' | 'warn' | 'info' | 'bad';

/**
 * The one thing to do next on a detail page: icon, title, a line of context and
 * the single action projected on the right. Disabled actions explain why.
 */
@Component({
  selector: 'ui-next-step',
  imports: [Icon],
  template: `
    <span class="next__icon" [class]="'next__icon next__icon--' + tone()"><ui-icon [name]="icon()" size="lg" /></span>
    <div class="next__text">
      <span class="next__eyebrow">{{ eyebrow() }}</span>
      <b class="next__title">{{ title() }}</b>
      @if (sub(); as text) {
        <span class="next__sub">{{ text }}</span>
      }
    </div>
    <div class="next__action"><ng-content /></div>
  `,
  styleUrl: './next-step.scss',
})
export class NextStep {
  readonly icon = input.required<IconName>();
  readonly tone = input<NextStepTone>('neutral');
  readonly title = input.required<string>();
  readonly sub = input<string | null>(null);
  readonly eyebrow = input('Next step');
}
