/** Offer data shape as returned by the Odisea catalog API. */
export interface OfferDto {
  id: string;
  title: string;
  country: string;          // ISO-3166-1 alpha-2 e.g. "GR"
  region?: string;          // sub-region e.g. "Санторини"
  city?: string;            // legacy
  cat?: string;             // "beach" | "tour" | "city" | "cruise" | "honey" | "safari"
  board?: string;           // "ai" | "fb" | "hb" | "bb" | "ro"
  boardBasis?: string;      // legacy: "AllInclusive" | "HalfBoard" | ...
  transport?: string;       // "plane" | "bus"
  price: number;
  currency?: string;        // legacy: "EUR"
  nights?: number;
  durationNights?: number;  // legacy
  rating?: number;
  op?: string;              // operator id
  months?: string[];
  img?: string;
  imageUrl?: string;        // legacy
  popular?: boolean;
  description?: string;
}

/** A publication / collection descriptor. */
export interface CollectionDto {
  id: string;
  title: string;
  slug?: string;
  count?: number;
  imageUrl?: string;
  description?: string;
}

/** Board-basis labels in Bulgarian. Covers both catalog ids and legacy enum names. */
export const BOARD_LABELS: Record<string, string> = {
  ai: 'All inclusive',
  fb: 'Пълен пансион',
  hb: 'Полупансион',
  bb: 'Закуска',
  ro: 'Без изхранване',
  AllInclusive: 'All inclusive',
  FullBoard: 'Пълен пансион',
  HalfBoard: 'Полупансион',
  BedAndBreakfast: 'Закуска',
  RoomOnly: 'Без изхранване',
};

/** Category labels in Bulgarian. */
export const CAT_LABELS: Record<string, string> = {
  beach: 'Морска почивка',
  tour: 'Обиколен тур',
  city: 'Градски уикенд',
  cruise: 'Круиз',
  honey: 'Меден месец',
  safari: 'Сафари',
};
