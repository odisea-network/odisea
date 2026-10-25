import { Pipe, PipeTransform } from '@angular/core';
import { formatMoney } from '../../core/format';

/** `{{ amount | money:currency }}` — display only; amounts arrive calculated from the API. */
@Pipe({ name: 'money' })
export class MoneyPipe implements PipeTransform {
  transform(amount: number | null | undefined, currency = 'EUR'): string {
    return formatMoney(amount, currency);
  }
}
