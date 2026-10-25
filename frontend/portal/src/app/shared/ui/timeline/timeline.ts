import { Component, input } from '@angular/core';
import { Icon } from '../icon/icon';
import { IconName } from '../icon/icons';

export interface TimelineItem {
  title: string;
  /** Text after the bold title, e.g. "by Elena Petrova". */
  detail?: string | null;
  /** Actor and time line. */
  meta: string;
  /** Free-text note shown in a quiet box. */
  note?: string | null;
  icon: IconName;
  tone?: 'neutral' | 'ok' | 'warn' | 'info' | 'bad';
}

/** Vertical history, newest first. */
@Component({
  selector: 'ui-timeline',
  imports: [Icon],
  template: `
    @for (item of items(); track $index) {
      <div class="tl__item">
        <span class="tl__dot" [class]="'tl__dot tl__dot--' + (item.tone ?? 'neutral')">
          <ui-icon [name]="item.icon" size="sm" />
        </span>
        <div class="tl__body">
          <div class="tl__text"><b>{{ item.title }}</b> {{ item.detail }}</div>
          <div class="tl__meta">{{ item.meta }}</div>
          @if (item.note) {
            <div class="tl__note">{{ item.note }}</div>
          }
        </div>
      </div>
    }
  `,
  styleUrl: './timeline.scss',
})
export class Timeline {
  readonly items = input.required<TimelineItem[]>();
}
