import { Component, booleanAttribute, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Icon } from '../icon/icon';

export interface Crumb {
  label: string;
  link?: string | unknown[];
}

/**
 * Title block of every page: optional breadcrumb, h1 with a `[status]` slot
 * beside it, a subtitle, and an `[actions]` slot aligned to the right.
 */
@Component({
  selector: 'ui-page-header',
  imports: [RouterLink, Icon],
  template: `
    <div class="ph__main">
      @if (crumbs().length > 0) {
        <nav class="ph__crumb" aria-label="Breadcrumb">
          @for (crumb of crumbs(); track $index; let last = $last) {
            @if (crumb.link) {
              <a [routerLink]="crumb.link">{{ crumb.label }}</a>
            } @else {
              <span>{{ crumb.label }}</span>
            }
            @if (!last) {
              <ui-icon name="chevron-right" size="sm" />
            }
          }
        </nav>
      }
      <div class="ph__title-row">
        <h1 class="ph__title" [class.num]="tabular()">{{ title() }}</h1>
        <ng-content select="[status]" />
      </div>
      @if (sub(); as text) {
        <p class="ph__sub">{{ text }}</p>
      }
    </div>
    <div class="ph__actions"><ng-content select="[actions]" /></div>
  `,
  styleUrl: './page-header.scss',
})
export class PageHeader {
  readonly title = input.required<string>();
  readonly sub = input<string | null>(null);
  readonly crumbs = input<Crumb[]>([]);
  /** Titles that are references (OD-26-10418) read better with tabular figures. */
  readonly tabular = input(false, { transform: booleanAttribute });
}
