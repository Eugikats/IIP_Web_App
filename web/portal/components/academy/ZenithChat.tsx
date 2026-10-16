"use client";

import Link from "next/link";
import { useEffect, useRef, useState, type ReactNode } from "react";
import { Icon } from "@/components/academy/Icon";
import { useAcademy } from "@/components/academy/AcademyProvider";
import { askZenith, loadZenithStatus, pickZenithQuiz, toggleZenithSteps, useZenith, type AgentMsg } from "@/lib/academy/zenithStore";

export const ZENITH_CHIPS = [
  "What should I study next?",
  "Quiz me",
  "What is the difference between generative AI and agentic AI?",
  "Plan my study week",
  "How many certificates have I earned?",
];

/** Zenith's text: bold, a plain list, paragraphs, and lesson citations such as [1] as small links to their lessons. */
function Rich({ text, sources }: { text: string; sources: AgentMsg["sources"] }) {
  const lines = text.split("\n");
  const blocks: ReactNode[] = [];
  let list: string[] = [];
  const flush = () => {
    if (list.length) {
      blocks.push(<ul key={`u${blocks.length}`}>{list.map((item, i) => <li key={i}>{inline(item)}</li>)}</ul>);
      list = [];
    }
  };
  const inline = (value: string): ReactNode[] => {
    const parts: ReactNode[] = [];
    const pattern = /\*\*(.+?)\*\*|\[(\d+)\]/g;
    let last = 0;
    let match: RegExpExecArray | null;
    let key = 0;
    while ((match = pattern.exec(value))) {
      if (match.index > last) parts.push(value.slice(last, match.index));
      if (match[1]) {
        parts.push(<b key={key++}>{match[1]}</b>);
      } else {
        const source = sources.find((entry) => entry.number === Number(match![2]));
        parts.push(source
          ? <Link key={key++} href={source.href} className="cite" title={source.label}>{match[2]}</Link>
          : <sup key={key++} className="cite">{match[2]}</sup>);
      }
      last = match.index + match[0].length;
    }
    if (last < value.length) parts.push(value.slice(last));
    return parts;
  };
  for (const line of lines) {
    if (line.startsWith("- ")) { list.push(line.slice(2)); continue; }
    flush();
    if (line.trim()) blocks.push(<p key={`p${blocks.length}`}>{inline(line)}</p>);
  }
  flush();
  return <>{blocks}</>;
}

function AgentMessage({ message, index }: { message: AgentMsg; index: number }) {
  const allDone = message.steps.length > 0 && message.steps.every((step) => step.state === "done");
  const collapsed = allDone && !message.open;
  return (
    <div className="msg a">
      <div className="aav"><Icon name="sparkle" size={15} /></div>
      <div className="abody">
        <div>
          {message.working ? (
            <div className="steps"><div className="sum working"><span className="spin" />Zenith is working...</div></div>
          ) : (
            <div className={`steps${collapsed ? " collapsed" : ""}${message.open ? " open" : ""}`}>
              {allDone ? (
                <button type="button" className="sum" onClick={() => toggleZenithSteps(index)}>
                  <Icon name="checkc" size={15} />Worked for {message.seconds?.toFixed(1) ?? "1.0"}s · {message.steps.length} steps<span className="chv"><Icon name="chevD" size={15} /></span>
                </button>
              ) : message.steps.length ? <div className="sum working"><span className="spin" />Zenith is working...</div> : null}
              <div className="slist">
                {message.steps.map((step, i) => (
                  <div key={i} className={`step ${step.state}`}>
                    <span className="si">{step.state === "done" ? <Icon name="check" size={15} /> : step.state === "run" ? <span className="spin" /> : <i />}</span>
                    {step.label}
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
        <div>
          {(allDone || message.mode === "error") && message.shown ? (
            <div className={`bub${message.mode === "error" ? " err" : ""}`}><Rich text={message.shown} sources={message.sources} />{message.done ? null : <span className="caret" />}</div>
          ) : null}
        </div>
        <div>
          {message.done ? (
            <>
              {message.mode === "unavailable" || message.mode === "resting" ? (
                <div className="notice" style={{ marginTop: 8 }}><Icon name="bulb" size={16} /><span>{message.mode === "resting" ? "Zenith is resting. The parts that cost nothing still work." : "Written explanations are not available right now."}</span></div>
              ) : null}
              {message.sources.length ? (
                <div className="srcs">
                  {message.sources.map((source) => (
                    <Link key={source.href} className="src" href={source.href}><Icon name="book" size={13} />{source.number ? `${source.number} · ` : ""}{source.label}</Link>
                  ))}
                </div>
              ) : null}
              {message.quiz ? (
                <div className="quiz">
                  <div className="qq">{message.quiz.q}</div>
                  {message.quiz.options.map((option, position) => {
                    const result = message.quiz!.result;
                    const cl = result ? (option.k === result.correctIndex ? "right" : option.k === message.quiz!.picked ? "wrong" : "") : option.k === message.quiz!.picked ? "sel" : "";
                    return (
                      <button key={option.k} type="button" className={`qopt ${cl}`} disabled={message.quiz!.picked !== undefined} onClick={() => void pickZenithQuiz(index, option.k)}>
                        {"ABCDEFGH"[position]}. {option.text}
                      </button>
                    );
                  })}
                  {message.quiz.result ? <div className="qfb"><b>{message.quiz.result.right ? "Correct!" : "Not quite."}</b> {message.quiz.result.why}</div> : null}
                  {message.quiz.result ? <div className="acts"><button type="button" className="btn soft sm" onClick={() => void askZenith("Quiz me again")}>Another question</button></div> : null}
                </div>
              ) : null}
              {message.actions.length ? (
                <div className="acts">
                  {message.actions.map((action) => action.href
                    ? <Link key={action.label} className="btn primary sm" href={action.href}>{action.label} <Icon name="arrowR" size={14} /></Link>
                    : <button key={action.label} type="button" className="btn soft sm" onClick={() => void askZenith(action.q ?? action.label)}>{action.label}</button>)}
                </div>
              ) : null}
            </>
          ) : null}
        </div>
      </div>
    </div>
  );
}

/** The chat log and its input, used by the floating panel and the full page. */
export function ZenithChat({ autoFocus = false }: { autoFocus?: boolean }) {
  const { me, learner } = useAcademy();
  const { msgs, busy, status } = useZenith();
  const [value, setValue] = useState("");
  const logRef = useRef<HTMLDivElement>(null);
  const input = useRef<HTMLInputElement>(null);

  useEffect(() => { void loadZenithStatus(); }, []);
  useEffect(() => {
    const box = logRef.current;
    if (box) box.scrollTop = box.scrollHeight;
  }, [msgs]);
  useEffect(() => { if (autoFocus && window.innerWidth > 860) input.current?.focus(); }, [autoFocus]);

  const name = (learner?.profile.name || (me?.signedIn ? me.name : "")).split(" ")[0] || "there";
  const submit = () => {
    const text = value.trim();
    if (!text || busy) return;
    setValue("");
    void askZenith(text);
  };

  return (
    <>
      <div className="chatlog" ref={logRef} data-chat="" role="log" aria-live="polite">
        {msgs.length === 0 ? (
          <div className="welcome">
            <div className="aav"><Icon name="sparkle" size={26} /></div>
            <h3>Hi {name}, I am Zenith</h3>
            <p>Your IIP Academy assistant. I can see your courses and progress. Ask me anything about your lessons or try one of these:</p>
            {status?.locked ? <div className="notice" style={{ margin: "0 0 12px", textAlign: "left" }} data-testid="zenith-locked"><Icon name="lock" size={16} /><span>Zenith is switched off while you are taking an assessment or working on your final task. It comes back when you finish or leave that page.</span></div> : null}
            {status && !status.configured ? <div className="notice" style={{ margin: "0 0 12px", textAlign: "left" }}><Icon name="bulb" size={16} /><span>Written explanations are not set up in this environment. Recommendations, plans and practice questions work.</span></div> : null}
            {status?.resting ? <div className="notice" style={{ margin: "0 0 12px", textAlign: "left" }}><Icon name="bulb" size={16} /><span>Zenith is resting for the rest of this month. Recommendations, plans and practice questions still work.</span></div> : null}
            <div className="chips">
              {ZENITH_CHIPS.map((chip) => <button key={chip} type="button" className="chip" onClick={() => void askZenith(chip)}>{chip}</button>)}
            </div>
          </div>
        ) : msgs.map((message, index) => (
          message.role === "user"
            ? <div key={index} className="msg u"><div className="bub">{message.text}</div></div>
            : <AgentMessage key={index} message={message} index={index} />
        ))}
      </div>
      <form className="chatform" onSubmit={(event) => { event.preventDefault(); submit(); }}>
        <input ref={input} value={value} onChange={(event) => setValue(event.target.value)} placeholder="Ask Zenith anything..." autoComplete="off" maxLength={500} aria-label="Ask Zenith" />
        <button className="send" type="submit" disabled={busy || !value.trim()} aria-label="Send"><Icon name="arrowUR" size={18} /></button>
      </form>
    </>
  );
}
