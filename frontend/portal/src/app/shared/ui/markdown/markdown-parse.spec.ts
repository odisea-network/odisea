import { describe, expect, it } from 'vitest';
import { parseInline, parseMarkdown } from './markdown-parse';

describe('parseMarkdown', () => {
  it('returns nothing for empty input', () => {
    expect(parseMarkdown(null)).toEqual([]);
    expect(parseMarkdown('  \n\n ')).toEqual([]);
  });

  it('splits headings, paragraphs and lists', () => {
    const blocks = parseMarkdown('Intro line one\nline two\n\n### Who it is for\n- Families\n- Couples\n\n1. Book\n2. Pay');
    expect(blocks.map(b => b.kind)).toEqual(['paragraph', 'heading', 'list', 'list']);
    expect(blocks[0]).toEqual({ kind: 'paragraph', inline: [{ text: 'Intro line one line two' }] });
    expect(blocks[1]).toMatchObject({ kind: 'heading', level: 4 });
    expect(blocks[2]).toMatchObject({ kind: 'list', ordered: false, items: [[{ text: 'Families' }], [{ text: 'Couples' }]] });
    expect(blocks[3]).toMatchObject({ kind: 'list', ordered: true });
  });

  it('maps # and ## to the larger heading level', () => {
    expect(parseMarkdown('## Getting there')[0]).toMatchObject({ kind: 'heading', level: 3 });
    expect(parseMarkdown('# Title')[0]).toMatchObject({ kind: 'heading', level: 3 });
  });

  it('joins an indented continuation line to the previous item', () => {
    const [list] = parseMarkdown('- **Documents**: passport valid\n  six months after return\n- Currency');
    expect(list).toMatchObject({
      kind: 'list',
      items: [[{ text: 'Documents', strong: true }, { text: ': passport valid six months after return' }], [{ text: 'Currency' }]],
    });
  });

  it('starts a new list when the marker type changes', () => {
    const blocks = parseMarkdown('- a\n1. b');
    expect(blocks).toHaveLength(2);
  });

  it('keeps HTML as literal text', () => {
    const [p] = parseMarkdown('<img src=x onerror=alert(1)>');
    expect(p).toEqual({ kind: 'paragraph', inline: [{ text: '<img src=x onerror=alert(1)>' }] });
  });
});

describe('parseInline', () => {
  it('reads bold, italic and link text', () => {
    expect(parseInline('Times are **indicative**, *subject* to [change](https://x.test).')).toEqual([
      { text: 'Times are ' },
      { text: 'indicative', strong: true },
      { text: ', ' },
      { text: 'subject', em: true },
      { text: ' to ' },
      { text: 'change' },
      { text: '.' },
    ]);
  });

  it('leaves a lone asterisk alone', () => {
    expect(parseInline('5 * 3 = 15')).toEqual([{ text: '5 * 3 = 15' }]);
  });
});
