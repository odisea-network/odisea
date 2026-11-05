import { NgTemplateOutlet } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import { parseMarkdown } from './markdown-parse';

/** Operator-written Markdown (descriptions, policies) as safe text: no HTML ever reaches the DOM. */
@Component({
  selector: 'ui-markdown',
  imports: [NgTemplateOutlet],
  template: `
    @for (block of blocks(); track $index) {
      @if (block.kind === 'heading') {
        @if (block.level === 3) {
          <h3 class="md-h"><ng-container [ngTemplateOutlet]="runs" [ngTemplateOutletContext]="{ $implicit: block.inline }" /></h3>
        } @else {
          <h4 class="md-h md-h--sub"><ng-container [ngTemplateOutlet]="runs" [ngTemplateOutletContext]="{ $implicit: block.inline }" /></h4>
        }
      } @else if (block.kind === 'paragraph') {
        <p><ng-container [ngTemplateOutlet]="runs" [ngTemplateOutletContext]="{ $implicit: block.inline }" /></p>
      } @else if (block.ordered) {
        <ol>
          @for (item of block.items; track $index) {
            <li><ng-container [ngTemplateOutlet]="runs" [ngTemplateOutletContext]="{ $implicit: item }" /></li>
          }
        </ol>
      } @else {
        <ul>
          @for (item of block.items; track $index) {
            <li><ng-container [ngTemplateOutlet]="runs" [ngTemplateOutletContext]="{ $implicit: item }" /></li>
          }
        </ul>
      }
    }
    <ng-template #runs let-parts>
      @for (part of parts; track $index) {
        @if (part.strong) {
          <strong>{{ part.text }}</strong>
        } @else if (part.em) {
          <em>{{ part.text }}</em>
        } @else {
          {{ part.text }}
        }
      }
    </ng-template>
  `,
  styleUrl: './markdown.scss',
})
export class Markdown {
  readonly text = input<string | null | undefined>(null);
  readonly blocks = computed(() => parseMarkdown(this.text()));
}
