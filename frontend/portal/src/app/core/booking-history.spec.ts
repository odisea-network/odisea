import { describe, expect, it } from 'vitest';
import { historyTimeline } from './booking-history';

describe('historyTimeline', () => {
  const history = [
    { at: '2026-10-25T00:58:00Z', from: '', to: 'Draft', note: null, actor: 'Agency' },
    { at: '2026-10-25T01:13:00Z', from: 'Draft', to: 'PriceConfirmed', note: 'Up €54.00 from search', actor: 'Agency' },
    { at: '2026-10-25T01:14:00Z', from: 'PriceConfirmed', to: 'AwaitingPayment', note: null, actor: 'System' },
  ];

  it('lists newest first with the status label, the actor and the note', () => {
    const items = historyTimeline(history);
    expect(items.map(i => i.title)).toEqual(['Awaiting payment', 'Price confirmed', 'Draft']);
    expect(items[0].meta).toContain('System');
    expect(items[1].note).toBe('Up €54.00 from search');
    expect(items[1].detail).toBe('from draft');
  });

  it('colours the milestones that matter and leaves the rest neutral', () => {
    const items = historyTimeline(history);
    expect(items[0].tone).toBe('warn');
    expect(items[1].tone).toBe('info');
    expect(items[2].tone).toBe('neutral');
  });

  it('shows no "from" for the first entry, whatever placeholder the API sends', () => {
    const [draft] = historyTimeline([{ at: '2026-10-25T00:58:00Z', from: '-', to: 'Draft', note: null, actor: 'Agency' }]);
    expect(draft.detail).toBeNull();
  });

  it('does not mutate the input', () => {
    const copy = [...history];
    historyTimeline(history);
    expect(history).toEqual(copy);
  });
});
