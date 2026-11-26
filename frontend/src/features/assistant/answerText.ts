/**
 * Light Markdown for assistant answers: paragraphs, bullet and numbered lists, **bold** and
 * numbered citations "[2]" / "[1, 3]" that point to sources[].n. Nothing else is interpreted,
 * so the text never becomes HTML.
 */
export type Block =
  | { kind: "paragraph"; text: string }
  | { kind: "list"; ordered: boolean; items: string[] };

export type Inline =
  | { kind: "text"; text: string }
  | { kind: "bold"; text: string }
  | { kind: "cite"; n: number };

const BULLET = /^[-*•]\s+/;
const NUMBERED = /^\d+[.)]\s+/;
const HEADING = /^#{1,6}\s+/;

export function parseBlocks(answer: string): Block[] {
  const blocks: Block[] = [];
  let paragraph: string[] = [];
  let list: { ordered: boolean; items: string[] } | null = null;

  const flush = () => {
    if (paragraph.length) blocks.push({ kind: "paragraph", text: paragraph.join(" ") });
    if (list) blocks.push({ kind: "list", ...list });
    paragraph = [];
    list = null;
  };

  for (const raw of answer.replace(/\r\n?/g, "\n").split("\n")) {
    const line = raw.trim();
    if (!line) {
      flush();
      continue;
    }
    const ordered = NUMBERED.test(line);
    if (ordered || BULLET.test(line)) {
      if (paragraph.length || (list && list.ordered !== ordered)) flush();
      list ??= { ordered, items: [] };
      list.items.push(line.replace(ordered ? NUMBERED : BULLET, ""));
      continue;
    }
    if (HEADING.test(line)) {
      flush();
      blocks.push({ kind: "paragraph", text: `**${line.replace(HEADING, "")}**` });
      continue;
    }
    if (list) flush();
    paragraph.push(line);
  }
  flush();
  return blocks;
}

const CITE = /\[(\d+(?:\s*[,;]\s*\d+)*)\]/g;

function withCitations(text: string, out: Inline[], kind: "text" | "bold") {
  let last = 0;
  for (const m of text.matchAll(CITE)) {
    const numbers = m[1].split(/[,;]/).map((s) => Number(s.trim())).filter((n) => n > 0);
    if (!numbers.length) continue;
    if (m.index > last) out.push({ kind, text: text.slice(last, m.index) });
    numbers.forEach((n) => out.push({ kind: "cite", n }));
    last = m.index + m[0].length;
  }
  if (last < text.length) out.push({ kind, text: text.slice(last) });
}

export function parseInline(text: string): Inline[] {
  const out: Inline[] = [];
  // Odd pieces sit between "**" pairs; an unclosed "**" (mid-stream) stays as text.
  const pieces = text.split("**");
  const closed = pieces.length % 2 === 1 ? pieces.length : pieces.length - 1;
  pieces.forEach((piece, i) => {
    if (!piece) return;
    if (i >= closed) withCitations("**" + piece, out, "text");
    else withCitations(piece, out, i % 2 === 1 ? "bold" : "text");
  });
  return out;
}

/** Numbers cited anywhere in the answer, in order of first appearance. */
export function citedNumbers(answer: string): number[] {
  const seen = new Set<number>();
  for (const m of answer.matchAll(CITE))
    m[1].split(/[,;]/).forEach((s) => {
      const n = Number(s.trim());
      if (n > 0) seen.add(n);
    });
  return [...seen];
}
