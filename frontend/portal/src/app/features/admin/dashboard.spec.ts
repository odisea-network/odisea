import { describe, expect, it } from 'vitest';
import { deltaLabel, kindMeta } from './dashboard';

describe('deltaLabel', () => {
  it('signs the change against the previous window', () => {
    expect(deltaLabel(94, 82, 7)).toBe('+12 спрямо предходните 7 дни');
    expect(deltaLabel(5, 9, 30)).toBe('−4 спрямо предходните 30 дни');
    expect(deltaLabel(3, 3, 7)).toBe('колкото в предходните 7 дни');
    expect(deltaLabel(2, 1, 1)).toBe('+1 спрямо вчера');
  });
});

describe('kindMeta', () => {
  it('gives every queue kind one action, confirm being the only primary one', () => {
    expect(kindMeta('ready')).toMatchObject({ action: 'Потвърди', primary: true, tone: 'ok' });
    expect(kindMeta('overdue')).toMatchObject({ action: 'Обяви за изтекла', primary: false, tone: 'bad' });
    expect(kindMeta('soon').action).toBe('Запиши плащане');
    expect(kindMeta('something-new').action).toBe('Отвори');
  });
});
