import {
  ItineraryDayDto, ProgramDetail, ReadinessCheckDto, SaveProgramContentRequest, TransportMode,
} from '../../core/api.types';
import { Meals, mealPlan, mealsOf } from '../../core/program-format';

export type ProgramTab = 'content' | 'itinerary' | 'gallery' | 'departures' | 'hotels';

/** Backend limits (SaveProgramContentRequest / ProgramContent), so the form can say so before the API does. */
export const CONTENT_LIMITS = {
  tagline: 160,
  summary: 600,
  description: 20_000,
  transportDetails: 4000,
  departurePoint: 200,
  practicalInfo: 8000,
  cancellationPolicy: 8000,
  paymentTerms: 4000,
  insurancePolicy: 4000,
  childPolicy: 2000,
  highlights: 12,
  listItems: 40,
  tags: 15,
  days: 60,
  dayTitle: 200,
  dayDescription: 4000,
  overnight: 200,
} as const;

/** Where each readiness check is fixed, so the panel can jump there. */
export const CHECK_TAB: Record<string, ProgramTab | 'details'> = {
  departures: 'departures',
  hotels: 'hotels',
  'transport-notes': 'departures',
  mappings: 'hotels',
  cover: 'gallery',
  gallery: 'gallery',
  'alt-text': 'gallery',
  'cover-size': 'gallery',
  itinerary: 'itinerary',
  title: 'details',
  description: 'details',
};

export function checkTab(check: Pick<ReadinessCheckDto, 'code'>): ProgramTab | 'details' {
  return CHECK_TAB[check.code] ?? 'content';
}

export interface DayDraft extends Meals {
  /** Stable key for @for tracking while days move. */
  key: number;
  title: string;
  description: string;
  overnight: string;
}

/** The content form: list fields edited as one item per line, tags comma-separated. */
export interface ContentDraft {
  tagline: string;
  summary: string;
  highlights: string;
  inclusions: string;
  exclusions: string;
  tags: string;
  transportMode: TransportMode;
  transportDetails: string;
  departurePoint: string;
  minGroupSize: number | null;
  maxGroupSize: number | null;
  practicalInfo: string;
  cancellationPolicy: string;
  paymentTerms: string;
  insurancePolicy: string;
  childPolicy: string;
  slug: string;
  itinerary: DayDraft[];
}

let nextKey = 1;

export function dayDraft(day?: ItineraryDayDto): DayDraft {
  return {
    key: nextKey++,
    title: day?.title ?? '',
    description: day?.description ?? '',
    ...mealsOf(day?.meals ?? 'None'),
    overnight: day?.overnight ?? '',
  };
}

export function draftFrom(p: ProgramDetail): ContentDraft {
  const c = p.content;
  return {
    tagline: c.tagline ?? '',
    summary: c.summary ?? '',
    highlights: c.highlights.join('\n'),
    inclusions: c.inclusions.join('\n'),
    exclusions: c.exclusions.join('\n'),
    tags: c.tags.join(', '),
    transportMode: c.transportMode,
    transportDetails: c.transportDetails ?? '',
    departurePoint: c.departurePoint ?? '',
    minGroupSize: c.minGroupSize,
    maxGroupSize: c.maxGroupSize,
    practicalInfo: c.practicalInfo ?? '',
    cancellationPolicy: c.cancellationPolicy ?? '',
    paymentTerms: c.paymentTerms ?? '',
    insurancePolicy: c.insurancePolicy ?? '',
    childPolicy: c.childPolicy ?? '',
    slug: p.slug,
    itinerary: [...p.itinerary].sort((a, b) => a.day - b.day).map(d => dayDraft(d)),
  };
}

/** One list item per non-empty line; a leading bullet ("- ", "• ", "* ") is dropped. */
export function lines(text: string): string[] {
  return text.split(/\r?\n/).map(l => l.replace(/^\s*(?:[-*•]\s+)/, '').trim()).filter(l => l.length > 0);
}

/** Comma- or line-separated tags as the API wants them: lowercase kebab-case, no repeats. */
export function tagsOf(text: string): string[] {
  const tags = text.split(/[,\n]/)
    .map(t => t.trim().toLowerCase().replace(/\s+/g, '-').replace(/-+/g, '-'))
    .filter(t => t.length > 0);
  return [...new Set(tags)];
}

function text(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

function size(value: number | null | string): number | null {
  if (value === null || value === '') return null;
  const n = Number(value);
  return Number.isFinite(n) && n > 0 ? Math.trunc(n) : null;
}

export function toRequest(d: ContentDraft, currentSlug: string): SaveProgramContentRequest {
  const slug = d.slug.trim().toLowerCase();
  return {
    tagline: text(d.tagline),
    summary: text(d.summary),
    highlights: lines(d.highlights),
    inclusions: lines(d.inclusions),
    exclusions: lines(d.exclusions),
    tags: tagsOf(d.tags),
    transportMode: d.transportMode,
    transportDetails: text(d.transportDetails),
    departurePoint: text(d.departurePoint),
    minGroupSize: size(d.minGroupSize),
    maxGroupSize: size(d.maxGroupSize),
    practicalInfo: text(d.practicalInfo),
    cancellationPolicy: text(d.cancellationPolicy),
    paymentTerms: text(d.paymentTerms),
    insurancePolicy: text(d.insurancePolicy),
    childPolicy: text(d.childPolicy),
    // Days are numbered by position, so moving a day renumbers the rest.
    itinerary: d.itinerary.map((day, i) => ({
      day: i + 1,
      title: day.title.trim(),
      description: text(day.description),
      meals: mealPlan(day),
      overnight: text(day.overnight),
    })),
    slug: slug === '' || slug === currentSlug ? null : slug,
  };
}

/** Problems the API would refuse, phrased for the form; empty when the draft can be saved. */
export function draftProblems(d: ContentDraft): string[] {
  const problems: string[] = [];
  const req = toRequest(d, d.slug);
  if (req.highlights.length > CONTENT_LIMITS.highlights) problems.push(`At most ${CONTENT_LIMITS.highlights} highlights.`);
  if (req.inclusions.length > CONTENT_LIMITS.listItems) problems.push(`At most ${CONTENT_LIMITS.listItems} included items.`);
  if (req.exclusions.length > CONTENT_LIMITS.listItems) problems.push(`At most ${CONTENT_LIMITS.listItems} excluded items.`);
  if (req.tags.length > CONTENT_LIMITS.tags) problems.push(`At most ${CONTENT_LIMITS.tags} tags.`);
  const badTag = req.tags.find(t => !/^[a-z0-9]+(-[a-z0-9]+)*$/.test(t) || t.length > 40);
  if (badTag) problems.push(`Tag "${badTag}" can only use letters a–z, digits and hyphens.`);
  if (req.minGroupSize !== null && req.maxGroupSize !== null && req.minGroupSize > req.maxGroupSize) {
    problems.push('Minimum group size is larger than the maximum.');
  }
  if (req.itinerary.length > CONTENT_LIMITS.days) problems.push(`An itinerary can have at most ${CONTENT_LIMITS.days} days.`);
  const untitled = req.itinerary.findIndex(day => day.title.length === 0);
  if (untitled >= 0) problems.push(`Day ${untitled + 1} needs a title.`);
  const slug = d.slug.trim().toLowerCase();
  if (slug !== '' && !/^[a-z0-9]+(-[a-z0-9]+)*$/.test(slug)) {
    problems.push('The web address can only use letters a–z, digits and single hyphens.');
  }
  return problems;
}

/** Days the itinerary should have: nights + 1 of the earliest departure, as the readiness check counts. */
export function expectedDays(p: Pick<ProgramDetail, 'departures'>): number | null {
  const first = [...p.departures].sort((a, b) => a.startDate.localeCompare(b.startDate))[0];
  return first ? first.nights + 1 : null;
}

export function moveDay(days: DayDraft[], index: number, by: -1 | 1): DayDraft[] {
  const target = index + by;
  if (target < 0 || target >= days.length) return days;
  const next = [...days];
  [next[index], next[target]] = [next[target], next[index]];
  return next;
}
