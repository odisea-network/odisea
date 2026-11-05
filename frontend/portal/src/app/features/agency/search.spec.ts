import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';
import { HotelOffer } from '../../core/api.types';
import { AgencySearch, sortOffers } from './search';

describe('AgencySearch', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
  });

  // Regression: departureId was a plain field once, so the memoized computed
  // never re-evaluated and the Search button stayed disabled forever.
  it('canSearch reacts to departure selection', () => {
    const fixture = TestBed.createComponent(AgencySearch);
    const component = fixture.componentInstance;

    expect(component.canSearch()).toBe(false);
    component.programId.set('p-1');
    expect(component.canSearch()).toBe(false);
    component.departureId.set('d-1');
    expect(component.canSearch()).toBe(true);

    component.selectProgram('p-2'); // switching programs resets the departure
    expect(component.canSearch()).toBe(false);
  });

  it('expired offers are flagged and not bookable', () => {
    const fixture = TestBed.createComponent(AgencySearch);
    const component = fixture.componentInstance;
    const offer = {
      hotelId: 'h', hotelName: 'H', stars: 4, roomType: 'DBL', board: 'HB',
      sellPrice: 1, currency: 'EUR', agencyCommission: 0, offerToken: 't',
      offerExpiresAt: new Date(Date.now() - 1000).toISOString(),
    };
    expect(component.expired(offer)).toBe(true);
    expect(component.expired({ ...offer, offerExpiresAt: null })).toBe(false);
  });
});

describe('sortOffers', () => {
  const offer = (hotel: string, price: number, stars: number, commission: number): HotelOffer => ({
    hotelId: hotel, hotelName: hotel, stars, roomType: 'DBL', board: 'HB', sellPrice: price, currency: 'EUR',
    agencyCommission: commission, offerToken: hotel, offerExpiresAt: null,
  });
  const offers = [offer('b', 1500, 4, 150), offer('a', 1100, 3, 110), offer('c', 2600, 5, 260)];

  it('sorts by price both ways', () => {
    expect(sortOffers(offers, 'price-asc').map(o => o.hotelName)).toEqual(['a', 'b', 'c']);
    expect(sortOffers(offers, 'price-desc').map(o => o.hotelName)).toEqual(['c', 'b', 'a']);
  });

  it('sorts by stars and commission, best first', () => {
    expect(sortOffers(offers, 'stars').map(o => o.hotelName)).toEqual(['c', 'b', 'a']);
    expect(sortOffers(offers, 'commission').map(o => o.hotelName)).toEqual(['c', 'b', 'a']);
  });

  it('never mutates the API result', () => {
    const copy = [...offers];
    sortOffers(offers, 'price-desc');
    expect(offers).toEqual(copy);
  });
});
