"use client";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useRef, useState, useEffect } from "react";
import { Sparkles, MessagesSquare, ShieldCheck, TrendingUp, AlertTriangle, ListChecks, Bot } from "lucide-react";
import { AIMessageBubble, UserMessageBubble, Composer } from "@/components/iip/ChatPrimitives";
import { CommandCenterBlocks } from "@/components/iip/CommandCenterAnswer";
import { useCommandCenterQuery, type CommandCenterAnswer } from "@/lib/lovable-parity/data/commandCenterQuery";
import { useOperationsSummary } from "@/lib/lovable-parity/data/reports";
import { useRuntimeContext } from "@/lib/runtimeContext";
import { useUI } from "@/context/UIStateContext";
import { getConversation } from "@/lib/lovable-parity/data/commandCenterConversations";

type Msg =
  | { id: string; role: "user"; text: string }
  | { id: string; role: "ai"; answer: CommandCenterAnswer; historical?: boolean }
  | { id: string; role: "ai-error"; text: string };

const STARTERS = [
  "Show claims that need attention.",
  "Which claims are missing documents?",
  "Show SLA breaches.",
  "Show pending approvals.",
];

export default function AIConversations({ conversationId: requestedConversationId }: { conversationId?: string }) {
  const router = useRouter();
  const { runtimeContext } = useRuntimeContext();
  const { roleDef } = useUI();
  const { ask, loading } = useCommandCenterQuery();
  const { summary } = useOperationsSummary();
  const [messages, setMessages] = useState<Msg[]>([]);
  const [input, setInput] = useState("");
  const [conversationId, setConversationId] = useState(requestedConversationId);
  const [historyLoading, setHistoryLoading] = useState(Boolean(requestedConversationId));
  const [historyError, setHistoryError] = useState<string | null>(null);
  const [canContinue, setCanContinue] = useState(true);
  const scrollRef = useRef<HTMLDivElement>(null);

  const hasContent = messages.length > 0 || input.length > 0 || loading;
  const givenName = runtimeContext?.displayName?.trim().split(/\s+/)[0];

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: "smooth" });
  }, [messages, loading]);

  useEffect(() => {
    if (!requestedConversationId || !runtimeContext) return;
    const controller = new AbortController();
    setHistoryLoading(true);
    setHistoryError(null);
    void getConversation(requestedConversationId, roleDef.key, runtimeContext.userId, controller.signal)
      .then((conversation) => {
        const restored = conversation.turns.flatMap<Msg>((turn) => [
          { id: `${turn.turnId}-question`, role: "user", text: turn.question },
          { id: turn.turnId, role: "ai", answer: { ...turn.response, question: turn.question }, historical: true },
        ]);
        setMessages(restored);
        setConversationId(conversation.conversationId);
        setCanContinue(conversation.canContinue);
      })
      .catch((reason: unknown) => {
        if (reason instanceof DOMException && reason.name === "AbortError") return;
        setHistoryError(reason instanceof Error ? reason.message : "Conversation could not be loaded.");
      })
      .finally(() => {
        if (!controller.signal.aborted) setHistoryLoading(false);
      });
    return () => controller.abort();
  }, [requestedConversationId, roleDef.key, runtimeContext]);

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
    const answer = await ask(q, conversationId);
    if (answer?.conversationId && !conversationId) {
      setConversationId(answer.conversationId);
      router.replace(`/app/conversations/${answer.conversationId}`);
    }
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
            <Sparkles className="h-3 w-3" /> IIP Operations · Command Center
          </div>
          <h1 className="text-[32px] leading-[40px] font-semibold text-navy tracking-tight">{givenName ? `Good morning, ${givenName}.` : "What needs attention in this workspace?"}</h1>
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
        <div className="mt-4 flex flex-wrap justify-center gap-2">
          <Link href="/app/work-items" className="inline-flex items-center gap-2 rounded-xl border border-cloud bg-white/75 px-3 py-2 text-[11.5px] font-medium text-navy focus-ring"><ListChecks className="h-3.5 w-3.5 text-info" /> Review & approvals</Link>
          <Link href="/app/flows" className="inline-flex items-center gap-2 rounded-xl border border-cloud bg-white/75 px-3 py-2 text-[11.5px] font-medium text-navy focus-ring"><Bot className="h-3.5 w-3.5 text-violet" /> Automation Studio</Link>
        </div>
      </div>

      <div ref={scrollRef} className="flex-1 overflow-y-auto">
        <div className="max-w-[780px] mx-auto w-full space-y-6 pb-6">
          {historyLoading && <div className="glass-subtle p-4 text-[12.5px] text-stone" role="status">Loading conversation history…</div>}
          {historyError && <div className="glass-subtle p-4 text-[12.5px] text-error" role="alert">{historyError}</div>}
          {requestedConversationId && !historyLoading && !historyError && (
            <div className="rounded-xl border border-info/20 bg-info/5 px-4 py-3 text-[12px] text-slate2">
              Historical responses are shown for continuity. IIP checks current permissions and live workspace data again when you continue.
            </div>
          )}
          {messages.map((m) =>
            m.role === "user" ? (
              <UserMessageBubble key={m.id} text={m.text} />
            ) : m.role === "ai" ? (
              <AIMessageBubble key={m.id}><CommandCenterBlocks blocks={m.answer.blocks} historical={m.historical} /></AIMessageBubble>
            ) : (
              <AIMessageBubble key={m.id}><p className="text-[13px] text-error">{m.text}</p></AIMessageBubble>
            )
          )}
          {loading && (
            <div className="flex items-center gap-2 text-[12.5px] text-violet2 animate-fade-in">
              <span className="h-2 w-2 rounded-full bg-violet2 animate-pulse" /> IIP is preparing a grounded response…
            </div>
          )}
        </div>
      </div>

      <div className="max-w-[780px] mx-auto w-full space-y-2 pt-4 pb-2 sticky bottom-0 bg-transparent">
        <Composer value={input} onChange={setInput} onSend={() => send()} disabled={loading || historyLoading || !canContinue || Boolean(historyError)} />
        <div className="text-[10.5px] text-stone text-center">IIP Operations assists — it never settles, denies, repudiates, pays, or accuses fraud. Human reviewers approve regulated decisions.</div>
      </div>
    </div>
  );
}
