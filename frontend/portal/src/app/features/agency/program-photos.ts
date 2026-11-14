import { Component, computed, input, signal, ChangeDetectionStrategy } from '@angular/core';
import { ProgramImageDto } from '../../core/api.types';
import { ButtonDirective, Icon } from '../../shared/ui';

/** Large photo with thumbnails; the cover first. */
@Component({
  selector: 'app-program-photos',
  imports: [ButtonDirective, Icon],
  template: `
    @if (current(); as img) {
      <figure class="main">
        <img [src]="img.url" [alt]="img.altText ?? ''" [width]="img.width" [height]="img.height" />
        @if (ordered().length > 1) {
          <button type="button" uiButton size="sm" iconOnly class="nav nav--prev" aria-label="Previous photo" (click)="step(-1)"><ui-icon name="chevron-left" size="sm" /></button>
          <button type="button" uiButton size="sm" iconOnly class="nav nav--next" aria-label="Next photo" (click)="step(1)"><ui-icon name="chevron-right" size="sm" /></button>
        }
        @if (img.caption) {
          <figcaption>{{ img.caption }}</figcaption>
        }
      </figure>
      @if (ordered().length > 1) {
        <div class="thumbs" role="group" aria-label="Photos">
          @for (t of ordered(); track t.id; let i = $index) {
            <button type="button" class="thumb" [class.thumb--on]="i === index()" [attr.aria-pressed]="i === index()"
              [attr.aria-label]="'Photo ' + (i + 1) + ' of ' + ordered().length" (click)="index.set(i)">
              <img [src]="t.url" alt="" loading="lazy" />
            </button>
          }
        </div>
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './program-photos.scss',
})
export class ProgramPhotos {
  readonly images = input.required<ProgramImageDto[]>();
  readonly index = signal(0);

  readonly ordered = computed(() => {
    const all = this.images();
    return [...all.filter(i => i.isCover), ...all.filter(i => !i.isCover)];
  });
  readonly current = computed(() => this.ordered()[Math.min(this.index(), this.ordered().length - 1)] ?? null);

  step(by: number): void {
    const n = this.ordered().length;
    this.index.set((this.index() + by + n) % n);
  }
}
