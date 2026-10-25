/**
 * Stroke icons from the design canvas (24×24 grid, 1.8 stroke). Kept as path
 * data so ui-icon can inline them: they take `currentColor`, need no font and
 * add nothing to the bundle beyond the paths actually referenced.
 */
export const ICONS = {
  // Navigation
  home: 'M3 10.5 12 3l9 7.5V20a1 1 0 0 1-1 1h-5v-6H9v6H4a1 1 0 0 1-1-1z',
  search: 'M4 11a7 7 0 1 0 14 0a7 7 0 1 0-14 0M20 20l-3.5-3.5',
  bookings: 'M5 4h14v16H5zM9 9h6M9 13h6M9 17h3',
  documents: 'M14 3H6v18h12V7zM14 3v4h4M9 13h6M9 17h6',
  'file': 'M14 3H6v18h12V7zM14 3v4h4',
  team: 'M5.5 8a3.5 3.5 0 1 0 7 0a3.5 3.5 0 1 0-7 0M2.5 20a6.5 6.5 0 0 1 13 0M16 4.5a3.5 3.5 0 0 1 0 7M18 14a6 6 0 0 1 3.5 6',
  building: 'M4 3h16v18H4zM9 7h1M14 7h1M9 11h1M14 11h1M9 15h1M14 15h1M10 21v-3h4v3',
  layers: 'M21 8 12 3 3 8v8l9 5 9-5zM3 8l9 5 9-5M12 13v8',
  percent: 'M19 5 5 19M4.5 7a2.5 2.5 0 1 0 5 0a2.5 2.5 0 1 0-5 0M14.5 17a2.5 2.5 0 1 0 5 0a2.5 2.5 0 1 0-5 0',
  settings: 'M4 6h10M18 6h2M4 12h4M12 12h8M4 18h12M20 18h0M14 6a2 2 0 1 0 4 0a2 2 0 1 0-4 0M8 12a2 2 0 1 0 4 0a2 2 0 1 0-4 0M16 18a2 2 0 1 0 4 0a2 2 0 1 0-4 0',
  'map-pin': 'M12 21s7-6.2 7-11.5a7 7 0 1 0-14 0C5 14.8 12 21 12 21zM9.5 9.5a2.5 2.5 0 1 0 5 0a2.5 2.5 0 1 0-5 0',
  hotel: 'M3 18V6M3 14h18v4M21 14v-3a3 3 0 0 0-3-3h-7v6M5 10a2 2 0 1 0 4 0a2 2 0 1 0-4 0',
  menu: 'M4 6h16M4 12h16M4 18h16',
  bell: 'M6 8a6 6 0 1 1 12 0c0 7 3 9 3 9H3s3-2 3-9M10.3 21a1.94 1.94 0 0 0 3.4 0',
  logout: 'M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9',
  user: 'M8 8a4 4 0 1 0 8 0a4 4 0 1 0-8 0M4 21a8 8 0 0 1 16 0',

  // Chevrons & arrows
  'chevron-down': 'm6 9 6 6 6-6',
  'chevron-up': 'm6 15 6-6 6 6',
  'chevron-right': 'm9 6 6 6-6 6',
  'chevron-left': 'm15 6-6 6 6 6',
  'arrow-right': 'M5 12h14M13 6l6 6-6 6',
  download: 'M12 4v11m0 0-4-4m4 4 4-4M5 20h14',
  plus: 'M12 5v14M5 12h14',
  x: 'M6 6l12 12M18 6 6 18',
  more: 'M5 12h.01M12 12h.01M19 12h.01',

  // Feedback
  check: 'M5 12l5 5 9-10',
  'check-circle': 'M3 12a9 9 0 1 0 18 0a9 9 0 1 0-18 0M8 12l3 3 5-6',
  'x-circle': 'M3 12a9 9 0 1 0 18 0a9 9 0 1 0-18 0M9 9l6 6M15 9l-6 6',
  'slash-circle': 'M3 12a9 9 0 1 0 18 0a9 9 0 1 0-18 0M5.6 5.6l12.8 12.8',
  'alert-triangle': 'M12 4L2.5 20h19zM12 10v4M12 17v.01',
  'alert-circle': 'M3 12a9 9 0 1 0 18 0a9 9 0 1 0-18 0M12 8v5M12 16v.01',
  info: 'M3 12a9 9 0 1 0 18 0a9 9 0 1 0-18 0M12 11v5M12 8v.01',
  clock: 'M3 12a9 9 0 1 0 18 0a9 9 0 1 0-18 0M12 7v5l3 2',
  stopwatch: 'M12 5a8 8 0 1 0 0 16a8 8 0 1 0 0-16M12 9v4M9 2h6',
  hourglass: 'M6 3h12M6 21h12M7 3v3l5 6 5-6V3M7 21v-3l5-6 5 6v3',
  lock: 'M5 11h14v10H5zM8 11V7a4 4 0 0 1 8 0v4',
  eye: 'M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12zM12 9a3 3 0 1 0 0 6a3 3 0 1 0 0-6',
  'eye-off': 'M3 3l18 18M10.6 10.6a2 2 0 0 0 2.8 2.8M9.9 5.1A10.4 10.4 0 0 1 12 5c6.5 0 10 7 10 7a17 17 0 0 1-3.2 4.2M6.6 6.6A17.5 17.5 0 0 0 2 12s3.5 7 10 7a10 10 0 0 0 4.4-1',
  refresh: 'M20 11a8 8 0 1 0-2.3 5.7M20 4v7h-7',
  'wifi-off': 'M2 8.5a15 15 0 0 1 20 0M5 12a10 10 0 0 1 14 0M8.5 15.5a5 5 0 0 1 7 0M12 19h.01M3 3l18 18',

  // Booking lifecycle
  pencil: 'M4 20h4L19 9l-4-4L4 16v4z',
  tag: 'M3 12V4h8l10 10-8 8zM7.5 7.5h.01',
  send: 'M5 12h14M13 6l6 6-6 6',
  card: 'M3 5h18v14H3zM3 10h18M7 15h4',
  mail: 'M3 5h18v14H3zM3 7l9 6 9-6',
  chat: 'M21 12a8 8 0 0 1-11.6 7.1L3 21l1.9-6.4A8 8 0 1 1 21 12z',
  star: 'm12 3 2.7 5.6 6.1.9-4.4 4.3 1 6.1L12 17l-5.4 2.9 1-6.1-4.4-4.3 6.1-.9z',
  calendar: 'M3 5h18v16H3zM3 10h18M8 3v4M16 3v4',
  box: 'M7 7h10v10H7zM3 12h4M17 12h4',
  key: 'M14.5 4a5.5 5.5 0 0 0-5.3 7L3 17.2V21h3.8l1.2-1.2v-2h2l1.5-1.5h2.5l1-1a5.5 5.5 0 1 0-.5-11.3zM16 8h.01',
} as const;

export type IconName = keyof typeof ICONS;
