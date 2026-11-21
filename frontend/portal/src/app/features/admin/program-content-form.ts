import { Component, input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { t } from '../../core/i18n/i18n';
import { TRANSPORT_MODES } from '../../core/program-format';
import { TPipe } from '../../shared/pipes/t.pipe';
import { Field, InputDirective, Pill } from '../../shared/ui';
import { CONTENT_LIMITS, ContentDraft, lines, tagsOf } from './program-content.model';

/** Every sell-ready section of a program. Edits the parent's draft in place; the parent saves. */
@Component({
  selector: 'app-program-content-form',
  imports: [FormsModule, TPipe, Field, InputDirective, Pill],
  templateUrl: './program-content-form.html',
  styleUrl: './program-content-form.scss',
})
export class ProgramContentForm {
  readonly draft = input.required<ContentDraft>();

  readonly limits = CONTENT_LIMITS;
  readonly modes = TRANSPORT_MODES;
  readonly markdownHint = t('programEditor.form.markdownHint');

  length(text: string, max: number, extra = ''): string {
    return `${extra ? `${extra} · ` : ''}${text.length.toLocaleString('bg-BG')} / ${max.toLocaleString('bg-BG')}`;
  }

  items(text: string, max: number, extra = ''): string {
    const n = lines(text).length;
    return `${extra ? `${extra} · ` : ''}${t('programEditor.form.perLine', { n, max })}`;
  }

  tags(text: string): string[] {
    return tagsOf(text);
  }
}
