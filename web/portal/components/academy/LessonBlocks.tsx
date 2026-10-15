import type { ReactNode } from "react";
import { Icon } from "@/components/academy/Icon";
import type { Block } from "@/lib/academy/types";

/** Bold (**) and italic (*) inside a line of lesson text. Nothing else is interpreted, and nothing is inserted as markup. */
export function Inline({ text }: { text: string }): ReactNode {
  const parts: ReactNode[] = [];
  const pattern = /\*\*([^*]+)\*\*|\*([^*]+)\*/g;
  let last = 0;
  let match: RegExpExecArray | null;
  let key = 0;
  while ((match = pattern.exec(text))) {
    if (match.index > last) parts.push(text.slice(last, match.index));
    parts.push(match[1] ? <b key={key++}>{match[1]}</b> : <i key={key++}>{match[2]}</i>);
    last = match.index + match[0].length;
  }
  if (last < text.length) parts.push(text.slice(last));
  return <>{parts}</>;
}

/** A lesson's content, in the design's look. */
export function LessonBlocks({ blocks }: { blocks: Block[] }) {
  return (
    <>
      {blocks.map((block, index) => {
        switch (block.type) {
          case "h":
            return <h3 key={index}>{block.text}</h3>;
          case "p":
            return <p key={index} style={{ marginTop: 10 }}><Inline text={block.text} /></p>;
          case "note":
            return <div key={index} className="notice blue" style={{ marginTop: 12 }}><Icon name="bulb" size={18} /><span>{block.text}</span></div>;
          case "list":
            return block.ordered ? (
              <ol key={index} className="olist">{block.items.map((item, i) => <li key={i}><Inline text={item} /></li>)}</ol>
            ) : (
              <ul key={index}>{block.items.map((item, i) => <li key={i}><Icon name="checkc" size={17} /><span><Inline text={item} /></span></li>)}</ul>
            );
          case "table":
            return (
              <div key={index} className="tablewrap">
                <table>
                  <thead><tr>{block.head.map((cell, i) => <th key={i}>{cell}</th>)}</tr></thead>
                  <tbody>{block.rows.map((row, r) => <tr key={r}>{row.map((cell, c) => <td key={c}><Inline text={cell} /></td>)}</tr>)}</tbody>
                </table>
              </div>
            );
          case "flow":
            return (
              <div key={index}>
                <div className="flowrow">
                  {block.steps.map((step, i) => (
                    <span key={i} className="flowstep"><span className="flowtext">{step}</span>{i < block.steps.length - 1 ? <Icon name="arrowR" size={16} /> : null}</span>
                  ))}
                </div>
                {block.note ? <p style={{ marginTop: 8, fontSize: 14, color: "var(--muted)" }}>{block.note}</p> : null}
              </div>
            );
          case "screen":
            return (
              <figure key={index} className="screenfig">
                <img src={`/academy/${block.file}`} alt={block.caption} />
                <figcaption>{block.caption}</figcaption>
                {block.callouts.length ? <ol className="olist callouts">{block.callouts.map((callout, i) => <li key={i}>{callout}</li>)}</ol> : null}
              </figure>
            );
          case "screenMissing":
            return <div key={index} className="notice" style={{ marginTop: 12 }}><Icon name="layers" size={18} /><span>Screen not yet available in IIP. {block.text}</span></div>;
          case "example":
            return (
              <div key={index} className="exbox">
                <b>{block.title}</b>
                <ol className="olist">{block.steps.map((step, i) => <li key={i}>{step}</li>)}</ol>
              </div>
            );
          case "takeaways":
            return (
              <div key={index}>
                <h3>Key takeaways</h3>
                <ul>{block.items.map((item, i) => <li key={i}><Icon name="checkc" size={17} /><span>{item}</span></li>)}</ul>
              </div>
            );
          default:
            return null;
        }
      })}
    </>
  );
}
