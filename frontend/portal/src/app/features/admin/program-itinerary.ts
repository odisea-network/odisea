import { Component, computed, input, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonDirective, Field, Icon, InputDirective } from '../../shared/ui';
import { CONTENT_LIMITS, ContentDraft, dayDraft, moveDay } from './program-content.model';

/** Day-by-day plan. Days are numbered by position; the parent saves them with the content. */
@Component({
  selector: 'app-program-itinerary',
  imports: [FormsModule, ButtonDirective, Field, Icon, InputDirective],
  template: `
    @let d = draft();
    <div class="row between head">
      <div>
        <h2 class="h2">Day by day</h2>
        <p class="small muted">{{ hint() }}</p>
      </div>
      <button type="button" uiButton size="sm" (click)="add()" [disabled]="d.itinerary.length >= limits.days">
        <ui-icon name="plus" />Add day
      </button>
    </div>

    @if (d.itinerary.length === 0) {
      <div class="empty col">
        <p class="small muted">No itinerary yet. One entry per day: arrival, each full day, departure.</p>
        @if (expected(); as n) {
          <button type="button" uiButton size="sm" (click)="start(n)">Start with {{ n }} empty days</button>
        }
      </div>
    }

    <ol class="days">
      @for (day of d.itinerary; track day.key; let i = $index, first = $first, last = $last) {
        <li class="day">
          <span class="day__n">Day {{ i + 1 }}</span>
          <div class="col grow day__body">
            <div class="two">
              <ui-field label="Title" [for]="'it-t-' + day.key">
                <input uiInput [id]="'it-t-' + day.key" [(ngModel)]="day.title" [maxlength]="limits.dayTitle" placeholder="e.g. Arrival in Antalya" />
              </ui-field>
              <ui-field label="Overnight" [for]="'it-o-' + day.key" optional>
                <input uiInput [id]="'it-o-' + day.key" [(ngModel)]="day.overnight" [maxlength]="limits.overnight" placeholder="Town, or blank on the last day" />
              </ui-field>
            </div>
            <ui-field label="What happens" [for]="'it-d-' + day.key" optional>
              <textarea uiInput [id]="'it-d-' + day.key" rows="2" [(ngModel)]="day.description" [maxlength]="limits.dayDescription"></textarea>
            </ui-field>
            <fieldset class="row wrap meals">
              <legend class="small strong">Meals</legend>
              <label class="row small"><input type="checkbox" class="ui-chk" [(ngModel)]="day.breakfast" />Breakfast</label>
              <label class="row small"><input type="checkbox" class="ui-chk" [(ngModel)]="day.lunch" />Lunch</label>
              <label class="row small"><input type="checkbox" class="ui-chk" [(ngModel)]="day.dinner" />Dinner</label>
            </fieldset>
          </div>
          <div class="col day__tools">
            <button type="button" uiButton="ghost" size="sm" iconOnly [attr.aria-label]="'Move day ' + (i + 1) + ' up'" [disabled]="first" (click)="move(i, -1)"><ui-icon name="chevron-up" size="sm" /></button>
            <button type="button" uiButton="ghost" size="sm" iconOnly [attr.aria-label]="'Move day ' + (i + 1) + ' down'" [disabled]="last" (click)="move(i, 1)"><ui-icon name="chevron-down" size="sm" /></button>
            <button type="button" uiButton="ghost" size="sm" iconOnly [attr.aria-label]="'Remove day ' + (i + 1)" (click)="remove(i)"><ui-icon name="x" size="sm" /></button>
          </div>
        </li>
      }
    </ol>
  `,
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './program-itinerary.scss',
})
export class ProgramItinerary {
  readonly draft = input.required<ContentDraft>();
  /** Nights + 1 of the earliest departure, or null without departures. */
  readonly expected = input<number | null>(null);

  readonly limits = CONTENT_LIMITS;

  readonly hint = computed(() => {
    const n = this.expected();
    return n === null
      ? 'Add a departure to see how many days the trip has.'
      : `The earliest departure is a ${n}-day trip, so the itinerary should have ${n} days.`;
  });

  add(): void {
    const d = this.draft();
    d.itinerary = [...d.itinerary, dayDraft()];
  }

  start(days: number): void {
    const d = this.draft();
    d.itinerary = Array.from({ length: Math.min(days, CONTENT_LIMITS.days) }, () => dayDraft());
  }

  move(index: number, by: -1 | 1): void {
    const d = this.draft();
    d.itinerary = moveDay(d.itinerary, index, by);
  }

  remove(index: number): void {
    const d = this.draft();
    d.itinerary = d.itinerary.filter((_, i) => i !== index);
  }
}
