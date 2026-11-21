import { NgTemplateOutlet } from '@angular/common';
import { Component, computed, input, output, signal } from '@angular/core';
import { ProgramReadinessDto, ReadinessCheckDto } from '../../core/api.types';
import { TPipe } from '../../shared/pipes/t.pipe';
import { ButtonDirective, Card, Icon, Progress } from '../../shared/ui';
import { ProgramTab, checkTab, checkTitle } from './program-content.model';

/** The server's readiness report: required checks gate Publish, recommended ones make the score. */
@Component({
  selector: 'app-program-readiness',
  imports: [NgTemplateOutlet, TPipe, ButtonDirective, Card, Icon, Progress],
  template: `
    <ui-card class="ready">
      <div class="row between">
        <h2 class="h2">{{ 'programEditor.readiness.title' | t }}</h2>
        <span class="small strong">{{ 'programEditor.readiness.score' | t: { score: readiness().score } }}</span>
      </div>
      <ui-progress [value]="readiness().score" [max]="100" [tone]="readiness().canPublish ? 'brand' : 'warn'" />

      <h3 class="eyebrow">{{ 'programEditor.readiness.required' | t: { done: requiredPassed(), total: required().length } }}</h3>
      <ul class="checks">
        @for (c of required(); track c.code) {
          <ng-container [ngTemplateOutlet]="row" [ngTemplateOutletContext]="{ $implicit: c }" />
        }
      </ul>

      <div class="row between">
        <h3 class="eyebrow">{{ 'programEditor.readiness.recommended' | t: { done: recommendedPassed(), total: recommended().length } }}</h3>
        @if (recommendedPassed() > 0) {
          <button type="button" uiButton="ghost" size="sm" (click)="showAll.set(!showAll())">{{ (showAll() ? 'programEditor.readiness.hideDone' : 'programEditor.readiness.showDone') | t }}</button>
        }
      </div>
      <ul class="checks">
        @for (c of shownRecommended(); track c.code) {
          <ng-container [ngTemplateOutlet]="row" [ngTemplateOutletContext]="{ $implicit: c }" />
        } @empty {
          <li class="small muted">{{ 'programEditor.readiness.allDone' | t }}</li>
        }
      </ul>

      <p class="small muted">
        @if (status() === 'Published') { {{ 'programEditor.readiness.publishedNote' | t }} }
        @else { {{ 'programEditor.readiness.draftNote' | t }} }
      </p>
    </ui-card>

    <ng-template #row let-c>
      <li class="check">
        <span class="check__dot" [class.check__dot--ok]="c.passed" [class.check__dot--bad]="!c.passed && c.required">
          <ui-icon [name]="c.passed ? 'check' : 'alert-circle'" size="sm" />
          <span class="sr-only">{{ (c.passed ? 'programEditor.readiness.done' : 'programEditor.readiness.missing') | t }}</span>
        </span>
        <div class="col grow">
          <b class="small">{{ title(c) }}</b>
          <span class="small muted">{{ c.detail }}</span>
        </div>
        @if (!c.passed) {
          <button type="button" uiButton="ghost" size="sm" (click)="go.emit(tabOf(c))">{{ 'programEditor.readiness.fix' | t }}</button>
        }
      </li>
    </ng-template>
  `,
  styleUrl: './program-readiness.scss',
})
export class AdminProgramReadiness {
  readonly readiness = input.required<ProgramReadinessDto>();
  readonly status = input.required<string>();
  readonly go = output<ProgramTab | 'details'>();

  readonly showAll = signal(false);

  readonly required = computed(() => this.readiness().checks.filter(c => c.required));
  readonly recommended = computed(() =>
    // Open items first, so the useful ones stay on screen.
    [...this.readiness().checks.filter(c => !c.required)].sort((a, b) => Number(a.passed) - Number(b.passed)));
  readonly requiredPassed = computed(() => this.required().filter(c => c.passed).length);
  readonly recommendedPassed = computed(() => this.recommended().filter(c => c.passed).length);
  readonly shownRecommended = computed(() =>
    this.showAll() ? this.recommended() : this.recommended().filter(c => !c.passed));

  readonly title = checkTitle;

  tabOf(check: ReadinessCheckDto): ProgramTab | 'details' {
    return checkTab(check);
  }
}
