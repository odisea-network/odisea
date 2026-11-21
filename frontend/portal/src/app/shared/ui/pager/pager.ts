import { Component, computed, input, output } from '@angular/core';
import { t } from '../../../core/i18n/i18n';
import { ButtonDirective } from '../button/button.directive';

/** Range summary + previous/next, for API-paged lists. */
@Component({
  selector: 'ui-pager',
  imports: [ButtonDirective],
  template: `
    <span class="pager__text">{{ summary() }}</span>
    <div class="pager__nav">
      <button type="button" uiButton size="sm" (click)="go(page() - 1)" [disabled]="page() <= 1">
        {{ previousLabel }}
      </button>
      <button type="button" uiButton size="sm" (click)="go(page() + 1)" [disabled]="page() >= pageCount()">
        {{ nextLabel }}
      </button>
    </div>
  `,
  styleUrl: './pager.scss',
})
export class Pager {
  protected readonly previousLabel = t('kit.previous');
  protected readonly nextLabel = t('kit.next');

  readonly page = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly total = input.required<number>();
  readonly pageChange = output<number>();

  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize())));

  protected readonly summary = computed(() => {
    const total = this.total();
    if (total === 0) return t('kit.noResults');
    const from = (this.page() - 1) * this.pageSize() + 1;
    const to = Math.min(total, this.page() * this.pageSize());
    return t('kit.range', { from, to, total });
  });

  protected go(page: number): void {
    if (page >= 1 && page <= this.pageCount()) this.pageChange.emit(page);
  }
}
