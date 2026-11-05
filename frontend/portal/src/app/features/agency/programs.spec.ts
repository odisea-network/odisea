import { describe, expect, it } from 'vitest';
import { ProgramListItem } from '../../core/api.types';
import { filterPrograms, popularTags } from './programs';

function program(over: Partial<ProgramListItem>): ProgramListItem {
  return {
    id: over.name ?? 'x',
    name: 'Program',
    slug: 'program',
    destinationId: 'd',
    providerCode: 'mock',
    season: null,
    description: null,
    tagline: null,
    summary: null,
    tags: [],
    coverImageUrl: null,
    status: 'Published',
    createdAt: '2026-10-01T00:00:00Z',
    destinationName: null,
    country: null,
    nextDeparture: null,
    upcomingDepartures: 0,
    ...over,
  };
}

const antalya = program({ name: 'Antalya Easter', destinationName: 'Antalya', country: 'Turkey', tags: ['beach', 'family'], nextDeparture: '2027-04-03' });
const rome = program({ name: 'Rome Weekend', destinationName: 'Rome', country: 'IT', tags: ['city'], nextDeparture: '2027-03-01' });
const past = program({ name: 'Bodrum Autumn', destinationName: 'Bodrum', country: 'TR', tags: ['beach'] });

describe('filterPrograms', () => {
  it('puts programs with dates first, soonest first', () => {
    expect(filterPrograms([past, antalya, rome], '', null).map(p => p.name)).toEqual(['Rome Weekend', 'Antalya Easter', 'Bodrum Autumn']);
  });

  it('matches every word against name, place and tags', () => {
    expect(filterPrograms([past, antalya, rome], 'turkey beach', null).map(p => p.name)).toEqual(['Antalya Easter']);
    expect(filterPrograms([past, antalya, rome], 'turkiye', null).map(p => p.name)).toEqual(['Bodrum Autumn']);
    expect(filterPrograms([past, antalya, rome], 'FAMILY', null).map(p => p.name)).toEqual(['Antalya Easter']);
    expect(filterPrograms([past, antalya, rome], 'italy', null).map(p => p.name)).toEqual(['Rome Weekend']);
  });

  it('narrows to one tag', () => {
    expect(filterPrograms([past, antalya, rome], '', 'city').map(p => p.name)).toEqual(['Rome Weekend']);
  });
});

describe('popularTags', () => {
  it('orders tags by how many programs use them', () => {
    expect(popularTags([past, antalya, rome])).toEqual(['beach', 'city', 'family']);
    expect(popularTags([past, antalya, rome], 1)).toEqual(['beach']);
  });
});
