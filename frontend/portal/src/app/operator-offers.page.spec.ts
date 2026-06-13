import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiService, OfferDto } from './api.service';
import { OperatorOffersPage } from './operator-offers.page';

const offer = (id: string, status: string): OfferDto => ({
  id, title: `Offer ${id}`, description: '', country: 'GR', city: 'Athens',
  price: 500, currency: 'EUR', boardBasis: 'HalfBoard', transport: 'Plane',
  durationNights: 7, imageUrl: '', visibility: 'PlatformShared', ownerType: 'Operator',
  status, tags: [],
});

function makeApi(overrides: Partial<Record<keyof ApiService, unknown>> = {}) {
  return {
    listMyOffers: vi.fn().mockReturnValue(of([offer('1', 'Draft'), offer('2', 'Published')])),
    createOffer: vi.fn().mockReturnValue(of(offer('3', 'Draft'))),
    updateOffer: vi.fn().mockReturnValue(of(offer('1', 'Draft'))),
    publishOffer: vi.fn().mockReturnValue(of(offer('1', 'Published'))),
    unpublishOffer: vi.fn().mockReturnValue(of(offer('2', 'Draft'))),
    bulkCreateOffers: vi.fn().mockReturnValue(of({ created: 2, errors: [] })),
    ...overrides,
  } as unknown as ApiService;
}

function make(api: ApiService): OperatorOffersPage {
  TestBed.configureTestingModule({ providers: [{ provide: ApiService, useValue: api }] });
  return TestBed.runInInjectionContext(() => new OperatorOffersPage());
}

describe('OperatorOffersPage', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('loads the operator’s own offers on construction', () => {
    const api = makeApi();
    const page = make(api);

    expect(api.listMyOffers).toHaveBeenCalled();
    expect(page.offers().length).toBe(2);
    expect(page.loading()).toBe(false);
  });

  it('create calls createOffer then reloads', () => {
    const api = makeApi();
    const page = make(api);
    page.form.title = 'New';
    page.form.country = 'TR';

    page.save();

    expect(api.createOffer).toHaveBeenCalled();
    expect(api.listMyOffers).toHaveBeenCalledTimes(2); // initial + reload
  });

  it('edit prefills the form and save routes to updateOffer', () => {
    const api = makeApi();
    const page = make(api);

    page.edit(offer('1', 'Draft'));
    expect(page.editingId()).toBe('1');
    expect(page.form.title).toBe('Offer 1');

    page.save();
    expect(api.updateOffer).toHaveBeenCalledWith('1', expect.objectContaining({ title: 'Offer 1' }));
  });

  it('publish + unpublish call the right endpoints', () => {
    const api = makeApi();
    const page = make(api);

    page.publish(offer('1', 'Draft'));
    expect(api.publishOffer).toHaveBeenCalledWith('1');

    page.unpublish(offer('2', 'Published'));
    expect(api.unpublishOffer).toHaveBeenCalledWith('2');
  });

  it('cancelEdit clears the editing state', () => {
    const page = make(makeApi());
    page.edit(offer('1', 'Draft'));
    page.cancelEdit();

    expect(page.editingId()).toBeNull();
    expect(page.form.title).toBe('');
  });

  it('importCsv parses rows, posts them, and reloads on success', () => {
    const api = makeApi();
    const page = make(api);
    page.csvText = 'title,country\nA,GR\nB,TR';

    page.importCsv();

    expect(api.bulkCreateOffers).toHaveBeenCalledWith(
      expect.arrayContaining([expect.objectContaining({ title: 'A', country: 'GR' })]),
    );
    expect(page.importResult()?.created).toBe(2);
    expect(api.listMyOffers).toHaveBeenCalledTimes(2); // initial + reload
  });

  it('importCsv with no data rows sets an error and does not call the API', () => {
    const api = makeApi();
    const page = make(api);
    page.csvText = 'title,country'; // header only

    page.importCsv();

    expect(api.bulkCreateOffers).not.toHaveBeenCalled();
    expect(page.error()).toBeTruthy();
  });
});
