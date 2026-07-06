"use client";
import { useRef, useState, useEffect } from "react";
import { Sparkles, MessagesSquare, ShieldCheck, TrendingUp, AlertTriangle } from "lucide-react";
import { AIMessageBubble, UserMessageBubble, Composer } from "@/components/iip/ChatPrimitives";
import { CommandCenterBlocks, type CommandCenterAnswer } from "@/components/iip/CommandCenterAnswer";
import { useCommandCenterQuery } from "@/lib/lovable-parity/data/commandCenterQuery";
import { useOperationsSummary } from "@/lib/lovable-parity/data/reports";
import { useUI } from "@/context/UIStateContext";

type Msg =
  | { id: string; role: "user"; text: string }
  | { id: string; role: "ai"; answer: CommandCenterAnswer }
  | { id: string; role: "ai-error"; text: string };

const STARTERS = [
  "Show claims that need attention.",
  "Which claims are missing documents?",
  "Show SLA breaches.",
  "Show pending approvals.",
];

export default function AIConversations() {
  const { roleDef } = useUI();
  const { ask, loading } = useCommandCenterQuery();
  const { summary } = useOperationsSummary();
  const [messages, setMessages] = useState<Msg[]>([]);
  const [input, setInput] = useState("");
  const scrollRef = useRef<HTMLDivElement>(null);

  const hasContent = messages.length > 0 || input.length > 0 || loading;

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: "smooth" });
  }, [messages, loading]);

  const metrics = summary
    ? [
        { label: "Open claims", value: summary.openClaims, icon: MessagesSquare },
        { label: "SLA at risk", value: summary.slaAtRisk, icon: AlertTriangle },
        { label: "Approvals pending", value: summary.pendingApprovals, icon: ShieldCheck },
        { label: "Escalations", value: summary.escalationsRequired, icon: TrendingUp },
      ]
    : [];

  async function send(question?: string) {
    const q = (question ?? input).trim();
    if (!q || loading) return;
    setMessages((m) => [...m, { id: crypto.randomUUID(), role: "user", text: q }]);
    setInput("");
    const answer = await ask(q);
    setMessages((m) =>
      answer
        ? [...m, { id: crypto.randomUUID(), role: "ai", answer }]
        : [...m, { id: crypto.randomUUID(), role: "ai-error", text: "That request could not be answered against this workspace right now." }]
    );
  }

  return (
    <div className="min-h-[calc(100vh-8rem)] flex flex-col -mt-2">
      {!hasContent && (
        <div className="max-w-[780px] mx-auto w-full text-center py-10 animate-fade-in">
          <div className="inline-flex items-center gap-2 rounded-full bg-[hsl(var(--violet)/0.08)] px-3 py-1 text-[11px] uppercase tracking-[0.14em] text-violet2 mb-4">
            <Sparkles className="h-3 w-3" /> AI Conversations
          </div>
          <h1 className="text-[32px] leading-[40px] font-semibold text-navy tracking-tight">Good morning, {roleDef.name.split(" ")[0]}.</h1>
          <p className="text-[14px] text-stone mt-2">Ask about claims, documents, SLA, approvals, or missing information. Answers are grounded in this workspace's real data — every regulated decision remains with a human.</p>
        </div>
      )}

      <div className={"max-w-[780px] mx-auto w-full transition-all duration-300 " + (hasContent ? "opacity-0 -translate-y-2 pointer-events-none h-0 overflow-hidden" : "opacity-100 translate-y-0 mb-8")}>
        {metrics.length > 0 ? (
          <div className="grid grid-cols-2 md:grid-cols-4 gap-3 mb-5">
            {metrics.map((m) => (
              <div key={m.label} className="glass-subtle p-4 hover-lift">
                <div className="flex items-center gap-1.5 text-[11.5px] text-stone"><m.icon className="h-3.5 w-3.5 text-info" />{m.label}</div>
                <div className="mt-1.5 text-[24px] leading-none font-semibold text-navy tracking-tight">{Number(m.value).toLocaleString()}</div>
              </div>
            ))}
          </div>
        ) : (
          <div className="glass-subtle p-4 mb-5 text-center text-[12px] text-stone">Operational metrics load from the reports service for this workspace.</div>
        )}
        <div className="flex flex-wrap gap-2 justify-center">
          {STARTERS.map((s) => (
            <button key={s} onClick={() => send(s)} className="text-[12.5px] text-slate2 rounded-full border border-cloud bg-white/80 px-3 py-1.5 hover:bg-white hover-lift transition">{s}</button>
          ))}
        </div>
      </div>

      <div ref={scrollRef} className="flex-1 overflow-y-auto">
        <div className="max-w-[780px] mx-auto w-full space-y-6 pb-6">
          {messages.map((m) =>
            m.role === "user" ? (
              <UserMessageBubble key={m.id} text={m.text} />
            ) : m.role === "ai" ? (
              <AIMessageBubble key={m.id}><CommandCenterBlocks blocks={m.answer.blocks} /></AIMessageBubble>
            ) : (
              <AIMessageBubble key={m.id}><p className="text-[13px] text-error">{m.text}</p></AIMessageBubble>
            )
          )}
          {loading && (
            <div className="flex items-center gap-2 text-[12.5px] text-violet2 animate-fade-in">
              <span className="h-2 w-2 rounded-full bg-violet2 animate-pulse" /> Copilot is working…
            </div>
          )}
        </div>
      </div>

      <div className="max-w-[780px] mx-auto w-full space-y-2 pt-4 pb-2 sticky bottom-0 bg-transparent">
        <Composer value={input} onChange={setInput} onSend={() => send()} disabled={loading} />
        <div className="text-[10.5px] text-stone text-center">IIP Copilot assists — it never settles, denies, repudiates, pays, or accuses fraud. Human reviewers approve regulated decisions.</div>
      </div>
    </div>
  );
}
