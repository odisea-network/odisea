import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Icon } from '../icon/icon';
import { IconName } from '../icon/icons';

export type KpiTone = 'ok' | 'warn' | 'bad' | 'info' | 'neutral';

/** Headline figure that links to the list behind it. */
@Component({
  selector: 'ui-kpi',
  imports: [RouterLink, Icon],
  template: `
    <a class="kpi" [routerLink]="link()" [queryParams]="queryParams()">
      <span class="kpi__k">
        <span class="kpi__ico" [class]="'kpi__ico kpi__ico--' + tone()"><ui-icon [name]="icon()" size="sm" /></span>
        {{ label() }}
      </span>
      <span class="kpi__v" [class.kpi__v--bad]="tone() === 'bad' && value() !== 0">{{ value() }}</span>
      <span class="kpi__f">{{ foot() }}<ui-icon name="chevron-right" size="sm" /></span>
    </a>
  `,
  styleUrl: './kpi.scss',
})
export class Kpi {
  readonly icon = input.required<IconName>();
  readonly tone = input<KpiTone>('neutral');
  readonly label = input.required<string>();
  readonly value = input.required<number | string>();
  readonly foot = input('');
  readonly link = input<string | unknown[]>('/admin/bookings');
  readonly queryParams = input<Record<string, string> | null>(null);
}
