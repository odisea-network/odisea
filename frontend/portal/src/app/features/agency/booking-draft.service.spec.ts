import { describe, expect, it } from 'vitest';
import { HotelOffer, SearchResponse } from '../../core/api.types';
import { BookingDraftService } from './booking-draft.service';

const offer: HotelOffer = {
  hotelId: 'h-1', hotelName: 'Mock Palace', stars: 5,
  roomType: 'DBL', board: 'HB', sellPrice: 831.1, currency: 'EUR',
  agencyCommission: 83.11, offerToken: 'tok', offerExpiresAt: null,
};

const search: SearchResponse = {
  programId: 'p-1', programName: 'Antalya Easter 2027', departureId: 'd-1',
  checkIn: '2027-03-30', checkOut: '2027-04-06', nights: 7, offers: [offer],
};

describe('BookingDraftService', () => {
  it('captures the offer, the occupancy and the search-time price for drift comparison', () => {
    const service = new BookingDraftService();
    service.start(search, offer, 2, 1);

    const context = service.context();
    expect(context?.offer.offerToken).toBe('tok');
    expect(context?.searchSellPrice).toBe(831.1);
    expect(context?.nights).toBe(7);
    expect(context?.adults).toBe(2);
    expect(context?.children).toBe(1);
  });

  it('clear() empties the context', () => {
    const service = new BookingDraftService();
    service.start(search, offer, 2, 0);
    service.clear();
    expect(service.context()).toBeNull();
  });
});
