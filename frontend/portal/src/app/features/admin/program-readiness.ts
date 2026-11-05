import { NgTemplateOutlet } from '@angular/common';
import { Component, computed, input, output, signal } from '@angular/core';
import { ProgramReadinessDto, ReadinessCheckDto } from '../../core/api.types';
import { ButtonDirective, Card, Icon, Progress } from '../../shared/ui';
import { ProgramTab, checkTab } from './program-content.model';

/** The server's readiness report: required checks gate Publish, recommended ones make the score. */
@Component({
  selector: 'app-program-readiness',
  imports: [NgTemplateOutlet, ButtonDirective, Card, Icon, Progress],
  template: `
    <ui-card class="ready">
      <div class="row between">
        <h2 class="h2">Ready to publish?</h2>
        <span class="small strong">Score {{ readiness().score }}/100</span>
      </div>
      <ui-progress [value]="readiness().score" [max]="100" [tone]="readiness().canPublish ? 'brand' : 'warn'" />

      <h3 class="eyebrow">Required · {{ requiredPassed() }} of {{ required().length }}</h3>
      <ul class="checks">
        @for (c of required(); track c.code) {
          <ng-container [ngTemplateOutlet]="row" [ngTemplateOutletContext]="{ $implicit: c }" />
        }
      </ul>

      <div class="row between">
        <h3 class="eyebrow">Recommended · {{ recommendedPassed() }} of {{ recommended().length }}</h3>
        @if (recommendedPassed() > 0) {
          <button type="button" uiButton="ghost" size="sm" (click)="showAll.set(!showAll())">{{ showAll() ? 'Hide done' : 'Show done' }}</button>
        }
      </div>
      <ul class="checks">
        @for (c of shownRecommended(); track c.code) {
          <ng-container [ngTemplateOutlet]="row" [ngTemplateOutletContext]="{ $implicit: c }" />
        } @empty {
          <li class="small muted">Every recommended check passes.</li>
        }
      </ul>

      <p class="small muted">
        @if (status() === 'Published') { Published: every active agency can search it. }
        @else { Publishing makes this program searchable by every active agency. Existing bookings stay. }
      </p>
    </ui-card>

    <ng-template #row let-c>
      <li class="check">
        <span class="check__dot" [class.check__dot--ok]="c.passed" [class.check__dot--bad]="!c.passed && c.required">
          <ui-icon [name]="c.passed ? 'check' : 'alert-circle'" size="sm" />
          <span class="sr-only">{{ c.passed ? 'Done' : 'Missing' }}</span>
        </span>
        <div class="col grow">
          <b class="small">{{ c.title }}</b>
          <span class="small muted">{{ c.detail }}</span>
        </div>
        @if (!c.passed) {
          <button type="button" uiButton="ghost" size="sm" (click)="go.emit(tabOf(c))">Fix</button>
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

  tabOf(check: ReadinessCheckDto): ProgramTab | 'details' {
    return checkTab(check);
  }
}
