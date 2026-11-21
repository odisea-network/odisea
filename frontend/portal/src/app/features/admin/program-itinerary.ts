import { Component, computed, input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { t } from '../../core/i18n/i18n';
import { TPipe } from '../../shared/pipes/t.pipe';
import { ButtonDirective, Field, Icon, InputDirective } from '../../shared/ui';
import { CONTENT_LIMITS, ContentDraft, dayDraft, moveDay } from './program-content.model';

/** Day-by-day plan. Days are numbered by position; the parent saves them with the content. */
@Component({
  selector: 'app-program-itinerary',
  imports: [FormsModule, TPipe, ButtonDirective, Field, Icon, InputDirective],
  template: `
    @let d = draft();
    <div class="row between head">
      <div>
        <h2 class="h2">{{ 'program.itinerary' | t }}</h2>
        <p class="small muted">{{ hint() }}</p>
      </div>
      <button type="button" uiButton size="sm" (click)="add()" [disabled]="d.itinerary.length >= limits.days">
        <ui-icon name="plus" />{{ 'programEditor.itinerary.addDay' | t }}
      </button>
    </div>

    @if (d.itinerary.length === 0) {
      <div class="empty col">
        <p class="small muted">{{ 'programEditor.itinerary.empty' | t }}</p>
        @if (expected(); as n) {
          <button type="button" uiButton size="sm" (click)="start(n)">{{ 'programEditor.itinerary.start' | t: { n: n } }}</button>
        }
      </div>
    }

    <ol class="days">
      @for (day of d.itinerary; track day.key; let i = $index, first = $first, last = $last) {
        <li class="day">
          <span class="day__n">{{ 'program.day' | t: { n: i + 1 } }}</span>
          <div class="col grow day__body">
            <div class="two">
              <ui-field [label]="'programEditor.itinerary.title' | t" [for]="'it-t-' + day.key">
                <input uiInput [id]="'it-t-' + day.key" [(ngModel)]="day.title" [maxlength]="limits.dayTitle" [placeholder]="'programEditor.itinerary.titlePlaceholder' | t" />
              </ui-field>
              <ui-field [label]="'programEditor.itinerary.overnight' | t" [for]="'it-o-' + day.key" optional>
                <input uiInput [id]="'it-o-' + day.key" [(ngModel)]="day.overnight" [maxlength]="limits.overnight" [placeholder]="'programEditor.itinerary.overnightPlaceholder' | t" />
              </ui-field>
            </div>
            <ui-field [label]="'programEditor.itinerary.description' | t" [for]="'it-d-' + day.key" optional>
              <textarea uiInput [id]="'it-d-' + day.key" rows="2" [(ngModel)]="day.description" [maxlength]="limits.dayDescription"></textarea>
            </ui-field>
            <fieldset class="row wrap meals">
              <legend class="small strong">{{ 'programEditor.itinerary.meals' | t }}</legend>
              <label class="row small"><input type="checkbox" class="ui-chk" [(ngModel)]="day.breakfast" />{{ 'programEditor.itinerary.breakfast' | t }}</label>
              <label class="row small"><input type="checkbox" class="ui-chk" [(ngModel)]="day.lunch" />{{ 'programEditor.itinerary.lunch' | t }}</label>
              <label class="row small"><input type="checkbox" class="ui-chk" [(ngModel)]="day.dinner" />{{ 'programEditor.itinerary.dinner' | t }}</label>
            </fieldset>
          </div>
          <div class="col day__tools">
            <button type="button" uiButton="ghost" size="sm" iconOnly [attr.aria-label]="'programEditor.itinerary.moveUp' | t: { n: i + 1 }" [disabled]="first" (click)="move(i, -1)"><ui-icon name="chevron-up" size="sm" /></button>
            <button type="button" uiButton="ghost" size="sm" iconOnly [attr.aria-label]="'programEditor.itinerary.moveDown' | t: { n: i + 1 }" [disabled]="last" (click)="move(i, 1)"><ui-icon name="chevron-down" size="sm" /></button>
            <button type="button" uiButton="ghost" size="sm" iconOnly [attr.aria-label]="'programEditor.itinerary.remove' | t: { n: i + 1 }" (click)="remove(i)"><ui-icon name="x" size="sm" /></button>
          </div>
        </li>
      }
    </ol>
  `,
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
      ? t('programEditor.itinerary.hintNone')
      : t('programEditor.itinerary.hintDays', { n });
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
