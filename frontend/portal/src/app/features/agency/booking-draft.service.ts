import { Injectable, signal } from '@angular/core';
import { HotelOffer, SearchResponse } from '../../core/api.types';

export interface DraftContext {
  programId: string;
  programName: string;
  departureId: string;
  checkIn: string;
  checkOut: string;
  nights: number;
  /** Occupancy the offer was priced for — the wizard needs exactly this many passengers. */
  adults: number;
  children: number;
  offer: HotelOffer;
  /** Sell price shown at search time — the wizard compares the re-price against it. */
  searchSellPrice: number;
}

/** Carries the chosen offer from the search screen into the booking wizard. */
@Injectable({ providedIn: 'root' })
export class BookingDraftService {
  private readonly contextSignal = signal<DraftContext | null>(null);
  readonly context = this.contextSignal.asReadonly();

  start(search: SearchResponse, offer: HotelOffer, adults: number, children: number): void {
    this.contextSignal.set({
      programId: search.programId,
      programName: search.programName,
      departureId: search.departureId,
      checkIn: search.checkIn,
      checkOut: search.checkOut,
      nights: search.nights,
      adults,
      children,
      offer,
      searchSellPrice: offer.sellPrice,
    });
  }

  clear(): void {
    this.contextSignal.set(null);
  }
}
