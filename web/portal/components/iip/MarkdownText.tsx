import React from "react";

/**
 * Minimal, dependency-free renderer for the markdown subset the assistant actually
 * emits: bold, italics, inline code, bullet/numbered lists, headings and paragraphs.
 *
 * Deliberately returns React elements rather than an HTML string — assistant output is
 * model-generated text, and building HTML for `dangerouslySetInnerHTML` would make it an
 * injection surface. React escapes text nodes, so nothing here can inject markup, and no
 * sanitiser dependency is required. Anything outside the supported subset degrades to
 * plain readable text rather than showing raw syntax.
 */

type InlineToken = { text: string; bold?: boolean; italic?: boolean; code?: boolean };

// Splits on **bold**, *italic* / _italic_, and `code`, keeping the delimiters as captures.
const INLINE_PATTERN = /(\*\*[^*]+\*\*|`[^`]+`|(?<![*\w])\*[^*\n]+\*(?!\*)|(?<![_\w])_[^_\n]+_(?!_))/g;

function tokenizeInline(text: string): InlineToken[] {
  const tokens: InlineToken[] = [];
  let lastIndex = 0;

  for (const match of text.matchAll(INLINE_PATTERN)) {
    const index = match.index ?? 0;
    if (index > lastIndex) {
      tokens.push({ text: text.slice(lastIndex, index) });
    }

    const raw = match[0];
    if (raw.startsWith("**")) {
      tokens.push({ text: raw.slice(2, -2), bold: true });
    } else if (raw.startsWith("`")) {
      tokens.push({ text: raw.slice(1, -1), code: true });
    } else {
      tokens.push({ text: raw.slice(1, -1), italic: true });
    }

    lastIndex = index + raw.length;
  }

  if (lastIndex < text.length) {
    tokens.push({ text: text.slice(lastIndex) });
  }

  return tokens;
}

function Inline({ text }: { text: string }) {
  return (
    <>
      {tokenizeInline(text).map((token, i) => {
        if (token.bold) {
          return (
            <strong key={i} className="font-bold text-dls-text-primary">
              {token.text}
            </strong>
          );
        }
        if (token.code) {
          return (
            <code key={i} className="rounded-dls-tight bg-dls-bg-page px-1 py-0.5 text-[12.5px] font-mono">
              {token.text}
            </code>
          );
        }
        if (token.italic) {
          return <em key={i}>{token.text}</em>;
        }
        return <React.Fragment key={i}>{token.text}</React.Fragment>;
      })}
    </>
  );
}

type ListItem = { text: string; depth: number };

const BULLET = /^(\s*)[-*+]\s+(.*)$/;
const NUMBERED = /^(\s*)\d+[.)]\s+(.*)$/;
const HEADING = /^(#{1,6})\s+(.*)$/;

export function MarkdownText({ content, className }: { content: string; className?: string }) {
  const lines = (content ?? "").replace(/\r\n/g, "\n").split("\n");
  const nodes: React.ReactNode[] = [];

  let listBuffer: ListItem[] = [];
  let listOrdered = false;
  let paragraphBuffer: string[] = [];

  const flushList = () => {
    if (listBuffer.length === 0) return;
    const items = listBuffer;
    const ordered = listOrdered;
    listBuffer = [];
    const ListTag = ordered ? "ol" : "ul";
    nodes.push(
      <ListTag
        key={`list-${nodes.length}`}
        className={`${ordered ? "list-decimal" : "list-disc"} pl-5 space-y-1 my-2`}
      >
        {items.map((item, i) => (
          <li key={i} style={item.depth > 0 ? { marginLeft: item.depth * 12 } : undefined}>
            <Inline text={item.text} />
          </li>
        ))}
      </ListTag>,
    );
  };

  const flushParagraph = () => {
    if (paragraphBuffer.length === 0) return;
    const text = paragraphBuffer.join(" ");
    paragraphBuffer = [];
    nodes.push(
      <p key={`p-${nodes.length}`} className="my-2 first:mt-0 last:mb-0">
        <Inline text={text} />
      </p>,
    );
  };

  for (const line of lines) {
    if (line.trim() === "") {
      flushParagraph();
      flushList();
      continue;
    }

    const heading = HEADING.exec(line);
    if (heading) {
      flushParagraph();
      flushList();
      nodes.push(
        <div key={`h-${nodes.length}`} className="font-bold text-dls-text-primary mt-3 mb-1 first:mt-0">
          <Inline text={heading[2]} />
        </div>,
      );
      continue;
    }

    const numbered = NUMBERED.exec(line);
    const bullet = numbered ? null : BULLET.exec(line);
    if (numbered || bullet) {
      flushParagraph();
      const match = (numbered ?? bullet)!;
      const ordered = Boolean(numbered);
      // A change of list style starts a new list rather than mixing markers.
      if (listBuffer.length > 0 && ordered !== listOrdered) {
        flushList();
      }
      listOrdered = ordered;
      listBuffer.push({ text: match[2], depth: Math.floor(match[1].length / 2) });
      continue;
    }

    flushList();
    paragraphBuffer.push(line.trim());
  }

  flushParagraph();
  flushList();

  return <div className={className}>{nodes}</div>;
}
