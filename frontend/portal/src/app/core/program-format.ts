import { MealPlan, TransportMode } from './api.types';
import { t } from './i18n/i18n';

export const TRANSPORT_MODES: { value: TransportMode; label: string }[] = [
  { value: 'Unspecified', label: t('program.transportMode.Unspecified') },
  { value: 'Flight', label: t('program.transportMode.Flight') },
  { value: 'Bus', label: t('program.transportMode.Bus') },
  { value: 'Train', label: t('program.transportMode.Train') },
  { value: 'Ship', label: t('program.transportMode.Ship') },
  { value: 'OwnTransport', label: t('program.transportMode.OwnTransport') },
  { value: 'Mixed', label: t('program.transportMode.Mixed') },
];

const regions = typeof Intl.DisplayNames === 'function' ? new Intl.DisplayNames(['bg'], { type: 'region' }) : null;

/** "Antalya, Турция": destinations store the country as an ISO code. */
export function placeName(name: string | null | undefined, country: string | null | undefined): string {
  if (!name) return '';
  if (!country) return name;
  let label = country;
  if (/^[A-Za-z]{2}$/.test(country)) {
    try {
      label = regions?.of(country.toUpperCase()) ?? country;
    } catch {
      label = country;
    }
  }
  return `${name}, ${label}`;
}

export function transportLabel(mode: TransportMode): string {
  return TRANSPORT_MODES.find(m => m.value === mode)?.label ?? mode;
}

export interface Meals {
  breakfast: boolean;
  lunch: boolean;
  dinner: boolean;
}

/** The API's flags string ('None', 'Breakfast, Dinner') as three switches. */
export function mealsOf(plan: MealPlan): Meals {
  const parts = new Set(plan.split(',').map(p => p.trim()));
  return { breakfast: parts.has('Breakfast'), lunch: parts.has('Lunch'), dinner: parts.has('Dinner') };
}

export function mealPlan(meals: Meals): MealPlan {
  const parts = [meals.breakfast && 'Breakfast', meals.lunch && 'Lunch', meals.dinner && 'Dinner'].filter(Boolean);
  return parts.length > 0 ? parts.join(', ') : 'None';
}

export function mealLabel(plan: MealPlan): string {
  const m = mealsOf(plan);
  const parts = [m.breakfast && t('program.meals.breakfast'), m.lunch && t('program.meals.lunch'), m.dinner && t('program.meals.dinner')].filter(Boolean) as string[];
  if (parts.length === 0) return t('program.meals.none');
  const text = parts.length === 1 ? parts[0] : parts.slice(0, -1).join(', ') + ` ${t('program.meals.and')} ` + parts.at(-1);
  return text.charAt(0).toUpperCase() + text.slice(1);
}
