import { Component, booleanAttribute, input } from '@angular/core';

/**
 * Glass surface. With `title`, renders a header row with an actions slot
 * (`<ng-container cardActions>`). `flush` removes the body padding for tables.
 */
@Component({
  selector: 'ui-card',
  template: `
    @if (title(); as heading) {
      <header class="card__head">
        <h2 class="card__title">{{ heading }}</h2>
        <div class="card__actions"><ng-content select="[cardActions]" /></div>
      </header>
    }
    <ng-content />
  `,
  styleUrl: './card.scss',
  host: {
    class: 'glass',
    '[class.card--flush]': 'flush()',
    '[class.card--solid]': 'solid()',
  },
})
export class Card {
  readonly title = input<string | null>(null);
  readonly flush = input(false, { transform: booleanAttribute });
  readonly solid = input(false, { transform: booleanAttribute });
}
