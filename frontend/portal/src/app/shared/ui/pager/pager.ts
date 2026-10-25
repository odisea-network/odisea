import { Component, computed, input, output } from '@angular/core';
import { ButtonDirective } from '../button/button.directive';

/** Range summary + previous/next, for API-paged lists. */
@Component({
  selector: 'ui-pager',
  imports: [ButtonDirective],
  template: `
    <span class="pager__text">{{ summary() }}</span>
    <div class="pager__nav">
      <button type="button" uiButton size="sm" (click)="go(page() - 1)" [disabled]="page() <= 1">
        Previous
      </button>
      <button type="button" uiButton size="sm" (click)="go(page() + 1)" [disabled]="page() >= pageCount()">
        Next
      </button>
    </div>
  `,
  styleUrl: './pager.scss',
})
export class Pager {
  readonly page = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly total = input.required<number>();
  readonly pageChange = output<number>();

  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize())));

  protected readonly summary = computed(() => {
    const total = this.total();
    if (total === 0) return 'No results';
    const from = (this.page() - 1) * this.pageSize() + 1;
    const to = Math.min(total, this.page() * this.pageSize());
    return `${from}–${to} of ${total}`;
  });

  protected go(page: number): void {
    if (page >= 1 && page <= this.pageCount()) this.pageChange.emit(page);
  }
}
