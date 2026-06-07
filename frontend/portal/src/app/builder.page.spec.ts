import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { EMPTY, of } from 'rxjs';

import { BuilderPage } from './builder.page';
import {
  ApiService,
  CollectionDto,
  OfferDto,
  PublicationDto,
} from './api.service';

// ── Fixtures ──────────────────────────────────────────────────────────────────

const o = (id: string, country: string, board: string, transport: string, price: number): OfferDto => ({
  id, title: `Offer ${id}`, description: '', country, city: 'City',
  price, currency: 'EUR', boardBasis: board, transport,
  durationNights: 7, imageUrl: '', visibility: 'Active', tags: [],
});

const OFFERS: OfferDto[] = [
  o('1', 'GR', 'AllInclusive',    'Plane', 800),
  o('2', 'TR', 'HalfBoard',       'Bus',   1200),
  o('3', 'GR', 'FullBoard',       'Own',   600),
  o('4', 'EG', 'AllInclusive',    'Plane', 1400),
  o('5', 'GR', 'BedAndBreakfast', 'Plane', 500),
];

const COL: CollectionDto = { id: 'col-1', agencyId: 'ag-1', name: 'Summer', slug: 'summer', status: 'Active' };

const PUB: PublicationDto = {
  id: 'pub-1', agencyId: 'ag-1', key: 'key_abc', collectionId: COL.id,
  themeId: null, experienceConfig: null, status: 'Draft',
  allowedDomains: [], version: 1, createdAt: '', updatedAt: '',
};

// ── Helper: create component under test ───────────────────────────────────────

function makeApi(overrides: Partial<Record<keyof ApiService, unknown>> = {}) {
  return {
    listCollections:    () => of<CollectionDto[]>([]),
    listOffers:         () => of<OfferDto[]>([]),
    listThemes:         () => of([]),
    resolveCollection:  () => EMPTY,
    createCollection:   () => of(COL),
    createPublication:  () => of(PUB),
    publishPublication: () => of({ ...PUB, status: 'Published', key: 'key_pub' }),
    ...overrides,
  };
}

async function setup(apiOverrides: Parameters<typeof makeApi>[0] = {}) {
  await TestBed.configureTestingModule({
    imports: [BuilderPage],
    providers: [
      provideRouter([]),
      { provide: ApiService, useValue: makeApi(apiOverrides) },
    ],
  }).compileComponents();

  const fixture = TestBed.createComponent(BuilderPage);
  const component = fixture.componentInstance;
  fixture.detectChanges(); // triggers ngOnInit
  return { fixture, component };
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('BuilderPage — step navigation', () => {
  it('starts on the collection step', async () => {
    const { component } = await setup();
    expect(component.step()).toBe('collection');
    expect(component.stepIdx()).toBe(0);
  });

  it('goStep() jumps to the requested step', async () => {
    const { component } = await setup();
    component.goStep('experience');
    expect(component.step()).toBe('experience');
    expect(component.stepIdx()).toBe(1);
  });

  it('goStep() to last step sets stepIdx to 3', async () => {
    const { component } = await setup();
    component.goStep('publish');
    expect(component.stepIdx()).toBe(3);
  });

  it('onNextOrPublish() advances sequentially through all four steps', async () => {
    const { component } = await setup();

    component.onNextOrPublish();
    expect(component.step()).toBe('experience');

    component.onNextOrPublish();
    expect(component.step()).toBe('theme');

    component.onNextOrPublish();
    expect(component.step()).toBe('publish');
  });

  it('onNextOrPublish() on publish step triggers doPublish (pick mode, collection pre-selected)', async () => {
    const { component } = await setup({
      listCollections: () => of([COL]),
      createPublication: () => of(PUB),
    });
    // of() resolves synchronously so ngOnInit has already set selectedColId

    component.goStep('publish');
    component.onNextOrPublish(); // calls doPublish → callCreatePublication (synchronous via of())

    expect(component.embedOpen()).toBe(true);
    expect(component.createdKey()).toBe('key_abc');
    expect(component.publishing()).toBe(false);
  });
});

describe('BuilderPage — filteredOffers computed', () => {
  it('returns all offers up to limit when no filters applied', async () => {
    const { component } = await setup();
    component.allOffers.set(OFFERS);
    component.rules.update(r => ({ ...r, limit: 10 }));
    expect(component.filteredOffers().length).toBe(5);
  });

  it('filters by country', async () => {
    const { component } = await setup();
    component.allOffers.set(OFFERS);
    component.rules.update(r => ({ ...r, countries: ['GR'], limit: 10 }));
    expect(component.filteredOffers().length).toBe(3); // o1, o3, o5
    expect(component.filteredOffers().every(o => o.country === 'GR')).toBe(true);
  });

  it('filters by board', async () => {
    const { component } = await setup();
    component.allOffers.set(OFFERS);
    component.rules.update(r => ({ ...r, boards: ['AllInclusive'], limit: 10 }));
    expect(component.filteredOffers().length).toBe(2); // o1, o4
  });

  it('filters by price max', async () => {
    const { component } = await setup();
    component.allOffers.set(OFFERS);
    component.rules.update(r => ({ ...r, priceMax: 900, limit: 10 }));
    expect(component.filteredOffers().length).toBe(3); // 800, 600, 500
    expect(component.filteredOffers().every(o => o.price <= 900)).toBe(true);
  });

  it('filters by transport', async () => {
    const { component } = await setup();
    component.allOffers.set(OFFERS);
    component.rules.update(r => ({ ...r, transport: 'Plane', limit: 10 }));
    expect(component.filteredOffers().length).toBe(3); // o1, o4, o5
    expect(component.filteredOffers().every(o => o.transport === 'Plane')).toBe(true);
  });

  it('respects the limit', async () => {
    const { component } = await setup();
    component.allOffers.set(OFFERS);
    component.rules.update(r => ({ ...r, limit: 2, priceMax: 9999 }));
    expect(component.filteredOffers().length).toBe(2);
  });

  it('sorts by price ascending', async () => {
    const { component } = await setup();
    component.allOffers.set(OFFERS);
    component.rules.update(r => ({ ...r, sort: 'price', limit: 10 }));
    const prices = component.filteredOffers().map(o => o.price);
    expect(prices).toEqual([...prices].sort((a, b) => a - b));
  });

  it('sorts by price descending', async () => {
    const { component } = await setup();
    component.allOffers.set(OFFERS);
    component.rules.update(r => ({ ...r, sort: 'priceD', limit: 10 }));
    const prices = component.filteredOffers().map(o => o.price);
    expect(prices).toEqual([...prices].sort((a, b) => b - a));
  });
});

describe('BuilderPage — previewOffers and collection mode', () => {
  it('defaults to pick mode', async () => {
    const { component } = await setup();
    expect(component.colMode()).toBe('pick');
  });

  it('in rules mode previewOffers === filteredOffers', async () => {
    const { component } = await setup();
    component.allOffers.set(OFFERS);
    component.colMode.set('rules');
    expect(component.previewOffers()).toEqual(component.filteredOffers());
  });

  it('in pick mode with no collectionOffers falls back to filteredOffers', async () => {
    const { component } = await setup();
    component.allOffers.set(OFFERS);
    component.colMode.set('pick');
    component.collectionOffers.set([]); // empty = no loaded offers
    expect(component.previewOffers()).toEqual(component.filteredOffers());
  });

  it('in pick mode with loaded collectionOffers uses those', async () => {
    const { component } = await setup();
    component.allOffers.set(OFFERS);
    component.colMode.set('pick');
    component.collectionOffers.set([OFFERS[0], OFFERS[1]]);
    expect(component.previewOffers()).toEqual([OFFERS[0], OFFERS[1]]);
  });

  it('pick mode respects limit when slicing collectionOffers', async () => {
    const { component } = await setup();
    component.colMode.set('pick');
    component.collectionOffers.set(OFFERS);
    component.rules.update(r => ({ ...r, limit: 3 }));
    expect(component.previewOffers().length).toBe(3);
  });
});

describe('BuilderPage — domain management', () => {
  it('addDomain() adds a trimmed, unique domain', async () => {
    const { component } = await setup();
    component.domainInput.set('  example.com  ');
    component.addDomain();
    expect(component.allowedDomains()).toEqual(['example.com']);
    expect(component.domainInput()).toBe('');
  });

  it('addDomain() does not add duplicates', async () => {
    const { component } = await setup();
    component.domainInput.set('example.com');
    component.addDomain();
    component.domainInput.set('example.com');
    component.addDomain();
    expect(component.allowedDomains()).toEqual(['example.com']);
  });

  it('removeDomain() removes the matching domain', async () => {
    const { component } = await setup();
    component.domainInput.set('a.com');
    component.addDomain();
    component.domainInput.set('b.com');
    component.addDomain();
    component.removeDomain('a.com');
    expect(component.allowedDomains()).toEqual(['b.com']);
  });
});

describe('BuilderPage — embed modal and canGetEmbed', () => {
  it('canGetEmbed is false before any publish', async () => {
    const { component } = await setup();
    expect(component.canGetEmbed()).toBe(false);
  });

  it('canGetEmbed is true once createdKey is set', async () => {
    const { component } = await setup();
    component.createdKey.set('test_key');
    expect(component.canGetEmbed()).toBe(true);
  });

  it('embedOpen can be toggled', async () => {
    const { component } = await setup();
    expect(component.embedOpen()).toBe(false);
    component.embedOpen.set(true);
    expect(component.embedOpen()).toBe(true);
    component.embedOpen.set(false);
    expect(component.embedOpen()).toBe(false);
  });

  it('publish in draft mode sets createdKey and opens embed modal', async () => {
    const { component } = await setup({
      listCollections: () => of([COL]),
      createPublication: () => of(PUB),
    });

    component.isDraft.set(true);
    component.goStep('publish');
    component.onNextOrPublish(); // synchronous via of()

    expect(component.createdKey()).toBe('key_abc');
    expect(component.embedOpen()).toBe(true);
  });
});

describe('BuilderPage — toggleArr helper', () => {
  it('adds a value to an empty array', async () => {
    const { component } = await setup();
    component.toggleArr('countries', 'GR');
    expect(component.rules().countries).toContain('GR');
  });

  it('removes a value already present', async () => {
    const { component } = await setup();
    component.toggleArr('countries', 'GR');
    component.toggleArr('countries', 'GR');
    expect(component.rules().countries).not.toContain('GR');
  });

  it('accumulates multiple values', async () => {
    const { component } = await setup();
    component.toggleArr('countries', 'GR');
    component.toggleArr('countries', 'TR');
    expect(component.rules().countries).toEqual(['GR', 'TR']);
  });
});
