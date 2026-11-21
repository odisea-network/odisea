import { Pipe, PipeTransform } from '@angular/core';
import { MessageKey, MessageParams, t } from '../../core/i18n/i18n';

/** `{{ 'nav.search' | t }}` — the portal's wording for a dictionary key. */
@Pipe({ name: 't' })
export class TPipe implements PipeTransform {
  transform(key: MessageKey, params?: MessageParams): string {
    return t(key, params);
  }
}
