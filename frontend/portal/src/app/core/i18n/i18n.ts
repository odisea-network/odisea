import { BG } from './bg';

type Leaves<T, Prefix extends string = ''> = {
  [K in keyof T & string]: T[K] extends string ? `${Prefix}${K}` : Leaves<T[K], `${Prefix}${K}.`>;
}[keyof T & string];

/** Every dotted path to a string in the dictionary; a typo is a compile error. */
export type MessageKey = Leaves<typeof BG>;

export type MessageParams = Record<string, string | number>;

function lookup(key: string): string | undefined {
  let node: unknown = BG;
  for (const part of key.split('.')) {
    if (typeof node !== 'object' || node === null) return undefined;
    node = (node as Record<string, unknown>)[part];
  }
  return typeof node === 'string' ? node : undefined;
}

function fill(text: string, params?: MessageParams): string {
  return params ? text.replace(/\{(\w+)\}/g, (whole, name: string) => String(params[name] ?? whole)) : text;
}

/** The portal's wording for a message. */
export function t(key: MessageKey, params?: MessageParams): string {
  return fill(lookup(key) ?? key, params);
}

/**
 * For keys built at run time from an API value (a status, an error code): the wording when the
 * dictionary has one, otherwise null so the caller can fall back.
 */
export function tOptional(key: string, params?: MessageParams): string | null {
  const text = lookup(key);
  return text === undefined ? null : fill(text, params);
}
