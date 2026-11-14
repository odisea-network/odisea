import { Component, input, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TRANSPORT_MODES } from '../../core/program-format';
import { Field, InputDirective, Pill } from '../../shared/ui';
import { CONTENT_LIMITS, ContentDraft, lines, tagsOf } from './program-content.model';

/** Every sell-ready section of a program. Edits the parent's draft in place; the parent saves. */
@Component({
  selector: 'app-program-content-form',
  imports: [FormsModule, Field, InputDirective, Pill],
  templateUrl: './program-content-form.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './program-content-form.scss',
})
export class ProgramContentForm {
  readonly draft = input.required<ContentDraft>();

  readonly limits = CONTENT_LIMITS;
  readonly modes = TRANSPORT_MODES;
  readonly markdownHint = 'Markdown: ## heading, - list, **bold**';

  length(text: string, max: number, extra = ''): string {
    return `${extra}${text.length.toLocaleString('en')} / ${max.toLocaleString('en')}`;
  }

  items(text: string, max: number, extra = ''): string {
    const n = lines(text).length;
    return `${extra}One per line · ${n} of ${max}`;
  }

  tags(text: string): string[] {
    return tagsOf(text);
  }
}
