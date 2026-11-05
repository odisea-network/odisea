import { describe, expect, it } from 'vitest';
import { ProgramDetail } from '../../core/api.types';
import { mealLabel, mealPlan, mealsOf, placeName, transportLabel } from '../../core/program-format';
import {
  checkTab, dayDraft, draftFrom, draftProblems, expectedDays, lines, moveDay, tagsOf, toRequest,
} from './program-content.model';
import { fileProblem } from './program-gallery';

const program: ProgramDetail = {
  id: 'p1',
  name: 'Antalya Easter 2027',
  slug: 'antalya-easter-2027',
  destinationId: 'd1',
  providerCode: 'mock',
  season: 'Easter 2027',
  description: null,
  status: 'Draft',
  content: {
    tagline: 'Seven sunny nights',
    summary: null,
    highlights: ['Direct charter', 'Beach hotels'],
    inclusions: ['Flight'],
    exclusions: [],
    tags: ['beach', 'family'],
    transportMode: 'Flight',
    transportDetails: null,
    departurePoint: 'Sofia Airport (SOF)',
    minGroupSize: null,
    maxGroupSize: 40,
    practicalInfo: null,
    cancellationPolicy: 'Free until 30 days before',
    paymentTerms: null,
    insurancePolicy: null,
    childPolicy: null,
  },
  itinerary: [
    { id: 'i2', day: 2, title: 'Beach', description: null, meals: 'Breakfast, Dinner', overnight: 'Antalya' },
    { id: 'i1', day: 1, title: 'Arrival', description: 'Transfer', meals: 'Dinner', overnight: 'Antalya' },
  ],
  images: [],
  departures: [
    { id: 'b', startDate: '2027-04-17', endDate: '2027-04-21', nights: 4, transportNote: null },
    { id: 'a', startDate: '2027-04-03', endDate: '2027-04-10', nights: 7, transportNote: 'FB 1471' },
  ],
  hotelIds: [],
  readiness: null,
  destination: null,
  hotels: [],
};

describe('content draft', () => {
  it('round-trips the stored content unchanged', () => {
    const draft = draftFrom(program);
    const request = toRequest(draft, program.slug);
    expect(request.highlights).toEqual(['Direct charter', 'Beach hotels']);
    expect(request.tags).toEqual(['beach', 'family']);
    expect(request.maxGroupSize).toBe(40);
    expect(request.summary).toBeNull();
    expect(request.slug).toBeNull();
    expect(request.itinerary.map(d => [d.day, d.title, d.meals])).toEqual([
      [1, 'Arrival', 'Dinner'],
      [2, 'Beach', 'Breakfast, Dinner'],
    ]);
  });

  it('numbers days by position after a move', () => {
    const draft = draftFrom(program);
    draft.itinerary = moveDay(draft.itinerary, 1, -1);
    expect(toRequest(draft, program.slug).itinerary.map(d => [d.day, d.title])).toEqual([[1, 'Beach'], [2, 'Arrival']]);
  });

  it('sends a slug only when it changed', () => {
    const draft = draftFrom(program);
    draft.slug = ' Antalya-Spring ';
    expect(toRequest(draft, program.slug).slug).toBe('antalya-spring');
  });

  it('turns blank text into null and empty group sizes into null', () => {
    const draft = draftFrom(program);
    draft.tagline = '   ';
    draft.maxGroupSize = null;
    const request = toRequest(draft, program.slug);
    expect(request.tagline).toBeNull();
    expect(request.maxGroupSize).toBeNull();
  });

  it('reports what the API would refuse', () => {
    const draft = draftFrom(program);
    draft.minGroupSize = 50;
    draft.tags = 'beach, café';
    draft.itinerary = [...draft.itinerary, dayDraft()];
    const problems = draftProblems(draft);
    expect(problems).toContain('Minimum group size is larger than the maximum.');
    expect(problems.some(p => p.includes('café'))).toBe(true);
    expect(problems).toContain('Day 3 needs a title.');
    expect(draftProblems(draftFrom(program))).toEqual([]);
  });
});

describe('list and tag parsing', () => {
  it('reads one item per line and drops bullets and blanks', () => {
    expect(lines('- Flight\n\n• Transfers \n  * Hotel')).toEqual(['Flight', 'Transfers', 'Hotel']);
  });

  it('normalises tags to kebab-case without repeats', () => {
    expect(tagsOf('All Inclusive, family,family\nbeach  holiday')).toEqual(['all-inclusive', 'family', 'beach-holiday']);
  });
});

describe('meals', () => {
  it('converts between the flags string and checkboxes', () => {
    expect(mealsOf('Breakfast, Dinner')).toEqual({ breakfast: true, lunch: false, dinner: true });
    expect(mealPlan({ breakfast: false, lunch: false, dinner: false })).toBe('None');
    expect(mealPlan({ breakfast: true, lunch: true, dinner: false })).toBe('Breakfast, Lunch');
  });

  it('labels meals and transport for people', () => {
    expect(mealLabel('None')).toBe('No meals');
    expect(mealLabel('Breakfast, Lunch, Dinner')).toBe('Breakfast, lunch and dinner');
    expect(transportLabel('Bus')).toBe('Coach');
  });

  it('spells out ISO country codes', () => {
    expect(placeName('Rome', 'it')).toBe('Rome, Italy');
    expect(placeName('Antalya', 'Türkiye')).toBe('Antalya, Türkiye');
    expect(placeName('Antalya', null)).toBe('Antalya');
    expect(placeName(null, 'TR')).toBe('');
  });
});

describe('readiness helpers', () => {
  it('expects nights + 1 days of the earliest departure', () => {
    expect(expectedDays(program)).toBe(8);
    expect(expectedDays({ departures: [] })).toBeNull();
  });

  it('sends each check to the tab that fixes it', () => {
    expect(checkTab({ code: 'cover' })).toBe('gallery');
    expect(checkTab({ code: 'description' })).toBe('details');
    expect(checkTab({ code: 'cancellation' })).toBe('content');
    expect(checkTab({ code: 'transport-notes' })).toBe('departures');
  });
});

describe('fileProblem', () => {
  it('accepts web images up to 5 MB only', () => {
    expect(fileProblem({ name: 'a.jpg', type: 'image/jpeg', size: 1000 })).toBeNull();
    expect(fileProblem({ name: 'a.svg', type: 'image/svg+xml', size: 1000 })).toContain('only PNG, JPEG or WebP');
    expect(fileProblem({ name: 'a.png', type: 'image/png', size: 6 * 1024 * 1024 })).toContain('larger than 5 MB');
    expect(fileProblem({ name: 'a.png', type: 'image/png', size: 0 })).toContain('empty');
  });
});
