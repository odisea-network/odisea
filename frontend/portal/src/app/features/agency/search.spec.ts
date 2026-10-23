import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';
import { AgencySearch } from './search';

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
