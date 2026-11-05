import { Component, booleanAttribute, computed, input, output } from '@angular/core';
import { HotelOffer } from '../../core/api.types';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { ButtonDirective, Icon, Pill } from '../../shared/ui';

/** One live-priced offer. `compact` is the phone layout (photo on the left). */
@Component({
  selector: 'app-offer-card',
  imports: [MoneyPipe, ButtonDirective, Icon, Pill],
  template: `
    <div class="offer__img" aria-hidden="true">
      <span class="offer__initial">{{ initial() }}</span>
      <ui-pill class="offer__av" [tone]="expired() ? 'bad' : 'ok'">{{ expired() ? 'Expired' : 'Available' }}</ui-pill>
    </div>
    <div class="offer__body">
      <div class="offer__head">
        <h3 class="offer__name">{{ offer().hotelName }}</h3>
        <span class="offer__stars" role="img" [attr.aria-label]="offer().stars + ' stars'">
          @for (star of stars(); track $index) {
            <ui-icon name="star" size="sm" />
          }
        </span>
      </div>
      <span class="small muted">{{ offer().roomType }} · <b>{{ offer().board }}</b> · {{ nights() }} nights</span>
      <div class="offer__foot">
        <div class="offer__price">
          <span class="m offer__total">{{ offer().sellPrice | money: offer().currency }}</span>
          <span class="small muted">total · you earn <b class="m">{{ offer().agencyCommission | money: offer().currency }}</b></span>
        </div>
        <div class="offer__actions">
          @if (!compact()) {
            <button
              type="button"
              [uiButton]="compared() ? 'ghost' : 'secondary'"
              size="sm"
              [attr.aria-pressed]="compared()"
              [disabled]="expired() || (!compared() && compareFull())"
              (click)="toggleCompare.emit()"
            >
              {{ compared() ? 'Added' : 'Compare' }}
            </button>
          }
          <button type="button" uiButton="primary" size="sm" [disabled]="expired()" (click)="book.emit()">Book</button>
        </div>
      </div>
    </div>
  `,
  styleUrl: './offer-card.scss',
  host: { class: 'glass', '[class.offer--compact]': 'compact()', '[class.offer--expired]': 'expired()' },
})
export class OfferCard {
  readonly offer = input.required<HotelOffer>();
  readonly nights = input.required<number>();
  readonly expired = input(false);
  readonly compared = input(false);
  readonly compareFull = input(false);
  readonly compact = input(false, { transform: booleanAttribute });
  readonly book = output<void>();
  readonly toggleCompare = output<void>();

  protected readonly stars = computed(() => Array.from({ length: this.offer().stars }));
  protected readonly initial = computed(() => this.offer().hotelName.trim().charAt(0).toUpperCase());
}
