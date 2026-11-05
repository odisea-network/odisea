/** A run of text inside a block; rendered through bindings, never as HTML. */
export interface MdInline {
  text: string;
  strong?: boolean;
  em?: boolean;
}

export type MdBlock =
  | { kind: 'heading'; level: 3 | 4; inline: MdInline[] }
  | { kind: 'paragraph'; inline: MdInline[] }
  | { kind: 'list'; ordered: boolean; items: MdInline[][] };

const HEADING = /^(#{1,6})\s+(.*)$/;
const BULLET = /^\s*[-*+]\s+(.*)$/;
const NUMBERED = /^\s*\d+[.)]\s+(.*)$/;
const INLINE = /(\*\*[^*]+\*\*|__[^_]+__|\*[^*\s][^*]*\*|_[^_\s][^_]*_|\[[^\]]+\]\([^)]*\))/;

/**
 * The Markdown subset program content is written in (see the create-program content guide):
 * headings, paragraphs, bullet and numbered lists, **bold**, *italic*. Links keep their text only.
 * Anything else stays literal text.
 */
export function parseMarkdown(source: string | null | undefined): MdBlock[] {
  const blocks: MdBlock[] = [];
  let paragraph: string[] = [];
  let list: { ordered: boolean; items: string[] } | null = null;

  const flushParagraph = () => {
    if (paragraph.length > 0) blocks.push({ kind: 'paragraph', inline: parseInline(paragraph.join(' ')) });
    paragraph = [];
  };
  const flushList = () => {
    if (list) blocks.push({ kind: 'list', ordered: list.ordered, items: list.items.map(parseInline) });
    list = null;
  };

  for (const raw of (source ?? '').replace(/\r\n?/g, '\n').split('\n')) {
    const line = raw.trimEnd();
    if (line.trim() === '') {
      flushParagraph();
      flushList();
      continue;
    }
    const heading = HEADING.exec(line.trim());
    if (heading) {
      flushParagraph();
      flushList();
      blocks.push({ kind: 'heading', level: heading[1].length <= 2 ? 3 : 4, inline: parseInline(heading[2].trim()) });
      continue;
    }
    const bullet = BULLET.exec(line);
    const numbered = bullet ? null : NUMBERED.exec(line);
    const item = bullet ?? numbered;
    if (item) {
      flushParagraph();
      const ordered = numbered !== null;
      if (list && list.ordered !== ordered) flushList();
      list ??= { ordered, items: [] };
      list.items.push(item[1].trim());
      continue;
    }
    if (list && /^\s/.test(raw)) {
      // An indented line continues the previous list item.
      list.items[list.items.length - 1] += ' ' + line.trim();
      continue;
    }
    flushList();
    paragraph.push(line.trim());
  }
  flushParagraph();
  flushList();
  return blocks;
}

export function parseInline(text: string): MdInline[] {
  const parts: MdInline[] = [];
  for (const token of text.split(INLINE)) {
    if (!token) continue;
    if ((token.startsWith('**') && token.endsWith('**')) || (token.startsWith('__') && token.endsWith('__'))) {
      parts.push({ text: token.slice(2, -2), strong: true });
    } else if (token.length > 2 && ((token.startsWith('*') && token.endsWith('*')) || (token.startsWith('_') && token.endsWith('_')))) {
      parts.push({ text: token.slice(1, -1), em: true });
    } else if (token.startsWith('[') && token.endsWith(')') && token.includes('](')) {
      parts.push({ text: token.slice(1, token.indexOf('](')) });
    } else {
      parts.push({ text: token });
    }
  }
  return parts;
}
