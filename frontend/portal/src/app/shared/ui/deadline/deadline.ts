import { Component, DestroyRef, computed, inject, input, signal } from '@angular/core';
import { formatDateTime, relativeDeadline } from '../../../core/format';
import { Icon } from '../icon/icon';

/**
 * A deadline told twice — how far away it is and exactly when — so urgency is
 * never carried by colour alone. Re-evaluates every minute while on screen.
 */
@Component({
  selector: 'ui-deadline',
  imports: [Icon],
  template: `
    @if (relative(); as rel) {
      <span class="dl__rel" [class]="'dl__rel u-' + rel.urgency">
        @if (rel.urgency === 'over') {
          <ui-icon name="alert-triangle" size="sm" />
        } @else if (rel.urgency === 'soon') {
          <ui-icon name="clock" size="sm" />
        }
        {{ rel.label }}
      </span>
      <span class="dl__abs">{{ absolute() }}</span>
    } @else {
      <span class="dl__abs">—</span>
    }
  `,
  styleUrl: './deadline.scss',
})
export class Deadline {
  readonly at = input.required<string | null | undefined>();

  private readonly now = signal(new Date());

  protected readonly relative = computed(() => relativeDeadline(this.at(), this.now()));
  protected readonly absolute = computed(() => formatDateTime(this.at()));

  constructor() {
    const timer = setInterval(() => this.now.set(new Date()), 60_000);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }
}
