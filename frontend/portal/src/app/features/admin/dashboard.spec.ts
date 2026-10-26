import { describe, expect, it } from 'vitest';
import { deltaLabel, kindMeta } from './dashboard';

describe('deltaLabel', () => {
  it('signs the change against the previous window', () => {
    expect(deltaLabel(94, 82, 7)).toBe('+12 vs the previous 7 days');
    expect(deltaLabel(5, 9, 30)).toBe('−4 vs the previous 30 days');
    expect(deltaLabel(3, 3, 7)).toBe('same as the previous 7 days');
    expect(deltaLabel(2, 1, 1)).toBe('+1 vs yesterday');
  });
});

describe('kindMeta', () => {
  it('gives every queue kind one action, confirm being the only primary one', () => {
    expect(kindMeta('ready')).toMatchObject({ action: 'Confirm', primary: true, tone: 'ok' });
    expect(kindMeta('overdue')).toMatchObject({ action: 'Expire', primary: false, tone: 'bad' });
    expect(kindMeta('soon').action).toBe('Record payment');
    expect(kindMeta('something-new').action).toBe('Open');
  });
});
