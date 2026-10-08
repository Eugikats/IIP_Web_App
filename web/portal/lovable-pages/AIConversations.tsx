"use client";
import { useRef, useState, useEffect, useMemo, useCallback } from "react";
import { useSearchParams } from "next/navigation";
import { motion } from "framer-motion";
import { ListChecks, AlertTriangle, CheckCircle2, CircleHelp, LineChart, UserPlus, FileText, ChevronRight } from "lucide-react";
import CcComposer from "@/components/command-center/CcComposer";
import TypingDots from "@/components/command-center/TypingDots";
import { CommandCenterBlocks } from "@/components/iip/CommandCenterAnswer";
import { useCommandCenterQuery, type CommandCenterAnswer } from "@/lib/lovable-parity/data/commandCenterQuery";
import { useOperationsSummary } from "@/lib/lovable-parity/data/reports";
import { useRuntimeContext } from "@/lib/runtimeContext";
import { useUI } from "@/context/UIStateContext";
import { getConversation, type ConversationGroundingSummary } from "@/lib/lovable-parity/data/commandCenterConversations";
import DataProvenance from "@/components/iip/DataProvenance";
import { Button } from "@/components/ui/Button";
import { LoadingSkeleton, ErrorStateSurface } from "@/components/ui/StateSurfaces";

type Msg =
  | { id: string; role: "user"; text: string }
  | { id: string; role: "ai"; answer: CommandCenterAnswer; historical?: boolean }
  | { id: string; role: "ai-error"; text: string; requestId?: string; question?: string };

type SuggestionChip = { icon: typeof ListChecks; text: string; question: string };

// Resolved in the browser's local timezone, which is the user's.
function timeOfDayGreeting(): string {
  const hour = new Date().getHours();
  if (hour < 5) return "Hello";
  if (hour < 12) return "Good morning";
  if (hour < 17) return "Good afternoon";
  return "Good evening";
}

// The Command Center, laid out as in CIP: a serif greeting, a round question box, example questions,
// then a thread of answers in bubbles. An answer can carry figures, a chart and a button to the screen
// where its records live. The conversation, history and retry behaviour are unchanged.
export default function AIConversations({ conversationId: requestedConversationId }: { conversationId?: string }) {
  const { runtimeContext } = useRuntimeContext();
  const { roleDef } = useUI();
  const { ask, loading } = useCommandCenterQuery();
  const { summary } = useOperationsSummary();
  const searchParams = useSearchParams();
  const autoSendRef = useRef(false);
  const [messages, setMessages] = useState<Msg[]>([]);
  const [input, setInput] = useState("");
  const [conversationId, setConversationId] = useState(requestedConversationId);
  const [historyLoading, setHistoryLoading] = useState(Boolean(requestedConversationId));
  const [historyError, setHistoryError] = useState<string | null>(null);
  const [historyRetryKey, setHistoryRetryKey] = useState(0);
  const [canContinue, setCanContinue] = useState(true);
  const [grounding, setGrounding] = useState<ConversationGroundingSummary | null>(null);
  const [pendingRequest, setPendingRequest] = useState<{ question: string; requestId: string } | null>(null);
  const scrollRef = useRef<HTMLDivElement>(null);
  const bottomRef = useRef<HTMLDivElement>(null);
  const restoredScrollRef = useRef(false);
  const skipDraftWriteRef = useRef(true);
  const storageKey = `iip.conversation.${requestedConversationId ?? conversationId ?? "new"}`;

  const hasContent = messages.length > 0 || input.length > 0 || loading || historyLoading || Boolean(historyError);
  const givenName = runtimeContext?.displayName?.trim().split(/\s+/)[0];

  useEffect(() => {
    skipDraftWriteRef.current = true;
    try { setInput(sessionStorage.getItem(`${storageKey}.draft`) ?? ""); } catch { /* best effort */ }
    restoredScrollRef.current = false;
  }, [storageKey]);

  useEffect(() => {
    if (skipDraftWriteRef.current) { skipDraftWriteRef.current = false; return; }
    try { sessionStorage.setItem(`${storageKey}.draft`, input); } catch { /* best effort */ }
  }, [input, storageKey]);

  useEffect(() => {
    if (historyLoading || restoredScrollRef.current || !scrollRef.current) return;
    scrollRef.current.scrollTo({ top: scrollRef.current.scrollHeight });
    restoredScrollRef.current = true;
  }, [historyLoading, messages.length, storageKey]);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [messages.length, loading]);

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
        setGrounding(conversation.grounding);
      })
      .catch((reason: unknown) => {
        if (reason instanceof DOMException && reason.name === "AbortError") return;
        setHistoryError(reason instanceof Error ? reason.message : "Conversation could not be loaded.");
      })
      .finally(() => {
        if (!controller.signal.aborted) setHistoryLoading(false);
      });
    return () => controller.abort();
  }, [requestedConversationId, roleDef.key, runtimeContext, historyRetryKey]);

  // Example questions, each one the Command Center answers from the workspace's own records.
  const chips = useMemo<SuggestionChip[]>(() => {
    const result: SuggestionChip[] = [
      { icon: UserPlus, text: "How many prospects came in today?", question: "How many customers were acquired today?" },
      { icon: LineChart, text: "Chart new prospects over the past week", question: "Chart the % increase in new prospects over the past week" },
    ];
    if (summary && summary.incompleteClaims > 0) {
      result.push({ icon: ListChecks, text: "Which claims are waiting on documents?", question: "Which claims are waiting on documents?" });
    } else {
      result.push({ icon: FileText, text: "Show claims by status", question: "Show claims by status" });
    }
    return result;
  }, [summary]);

  const retryHistory = useCallback(() => setHistoryRetryKey((key) => key + 1), []);

  async function send(question?: string, retryRequestId?: string) {
    const q = (question ?? input).trim();
    if (!q || loading) return;
    const requestId = retryRequestId ?? crypto.randomUUID();
    setPendingRequest({ question: q, requestId });
    setInput("");
    setMessages((current) => retryRequestId
      ? current.filter((message) => !(message.role === "ai-error" && message.requestId === retryRequestId))
      : [...current, { id: crypto.randomUUID(), role: "user", text: q }]);
    const result = await ask(q, conversationId, requestId);
    if (!result.ok) {
      setMessages((current) => [...current, { id: crypto.randomUUID(), role: "ai-error", text: result.message, requestId: result.kind === "reconcile" ? requestId : undefined, question: q }]);
      if (result.kind !== "reconcile") setPendingRequest(null);
      return;
    }
    const answer = result.answer;
    if (answer?.conversationId && !conversationId) {
      setConversationId(answer.conversationId);
      // Update the address without remounting: a route swap reloaded the conversation from history and
      // relabelled the answer just given as historical with its actions disabled.
      window.history.replaceState(window.history.state, "", `/app/conversations/${answer.conversationId}`);
    }
    setMessages((current) => [...current, { id: crypto.randomUUID(), role: "ai", answer }]);
    setPendingRequest(null);
  }

  // A question typed into the persistent Ask bar on another screen arrives here as ?q=... and is sent
  // once, then the query string is cleared so a refresh does not resend it.
  useEffect(() => {
    const question = searchParams.get("q");
    if (!question || autoSendRef.current || requestedConversationId) return;
    autoSendRef.current = true;
    window.history.replaceState(window.history.state, "", window.location.pathname);
    void send(question);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchParams, requestedConversationId]);

  return (
    <div className="cip-cc -mx-6 -mb-[60px] -mt-7 flex min-h-[calc(100dvh-3rem)] flex-col px-4 pb-4 pt-7 sm:px-6">
      {!hasContent && (
        <div className="mx-auto flex w-full max-w-3xl flex-1 flex-col">
          <div className="flex-1" aria-hidden="true" />
          <motion.header
            initial={{ opacity: 0, y: 8 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.4, ease: [0.22, 1, 0.36, 1] }}
            className="shrink-0 text-center"
          >
            <h1 className="cip-cc-serif m-0 text-balance text-[1.85rem] font-semibold leading-tight tracking-tight text-[#0B1640] sm:text-[2.35rem]">
              {timeOfDayGreeting()}
              {givenName ? <>, <span className="text-[#1769FF]">{givenName}</span></> : null}
            </h1>
            <p className="cip-cc-serif mx-auto mt-3 max-w-lg text-[15px] leading-relaxed text-slate-500 sm:text-base">
              Your AI analyst for claims, customers and acquisition. Ask a question, or ask for a chart.
            </p>
          </motion.header>
          <div className="flex-1" aria-hidden="true" />
          <div className="w-full shrink-0">
            <CcComposer value={input} onChange={setInput} onSend={() => void send()} disabled={loading} />
          </div>
          <section className="mx-auto mt-10 w-full max-w-4xl pb-2" aria-label="Questions to try">
            <div className="mb-5 flex items-center gap-3">
              <div className="h-px flex-1 bg-slate-200/80" />
              <p className="m-0 shrink-0 text-[11px] font-semibold uppercase tracking-[0.14em] text-slate-400">Try asking</p>
              <div className="h-px flex-1 bg-slate-200/80" />
            </div>
            <div className="grid gap-2 lg:grid-cols-3">
              {chips.map((chip, index) => (
                <motion.button
                  key={chip.text}
                  type="button"
                  onClick={() => void send(chip.question)}
                  initial={{ opacity: 0, y: 8 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ duration: 0.35, delay: 0.1 + index * 0.06, ease: [0.22, 1, 0.36, 1] }}
                  className="flex w-full items-center gap-3 rounded-xl px-2 py-2 text-left transition hover:bg-slate-50/80"
                >
                  <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-blue-50">
                    <chip.icon className="h-5 w-5 text-[#1769FF]" aria-hidden="true" />
                  </span>
                  <span className="min-w-0 flex-1 text-[14px] font-semibold text-[#0B1640]">{chip.text}</span>
                  <ChevronRight className="h-4 w-4 shrink-0 text-slate-300 lg:hidden" aria-hidden="true" />
                </motion.button>
              ))}
            </div>
          </section>
          <div className="h-[min(8vh,4rem)] shrink-0" aria-hidden="true" />
        </div>
      )}

      <div ref={scrollRef} className={hasContent ? "min-h-0 flex-1 overflow-y-auto" : "hidden"}>
        <div className="mx-auto w-full max-w-3xl space-y-4 pb-6 pt-3 sm:space-y-5 sm:pt-5">
          {historyLoading && <LoadingSkeleton rows={2} />}
          {historyError && <ErrorStateSurface message={historyError} onRetry={retryHistory} />}
          {requestedConversationId && !historyLoading && !historyError && (
            <div className="rounded-xl border border-slate-100 bg-white px-4 py-3 text-[12.5px] text-slate-500">
              Earlier answers are shown for continuity. IIP checks current permissions and live workspace data again when you continue.
            </div>
          )}
          {grounding && !historyLoading && !historyError ? (
            <div
              role={grounding.status === "changed" ? "alert" : "status"}
              className={
                "rounded-xl border px-4 py-3 text-[12.5px] " +
                (grounding.status === "changed" ? "border-amber-100 bg-amber-50 text-amber-800" : "border-slate-100 bg-white text-slate-600")
              }
            >
              <div className="flex items-center gap-1.5 font-semibold">
                {grounding.status === "current" ? (
                  <CheckCircle2 className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
                ) : grounding.status === "changed" ? (
                  <AlertTriangle className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
                ) : (
                  <CircleHelp className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
                )}
                {grounding.status === "current" ? "Evidence checked, no recorded changes" : grounding.status === "changed" ? "Evidence changed since this answer" : "Current evidence could not be checked"}
              </div>
              <div className="mt-1">{grounding.safeChangeSummary}</div>
              <div className="mt-1 text-[10.5px] text-slate-400">Saved {new Date(grounding.historicalTimestamp).toLocaleString()}</div>
            </div>
          ) : null}
          {messages.map((m) =>
            m.role === "user" ? (
              <motion.div key={m.id} initial={{ opacity: 0, y: 6 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.25 }} className="flex justify-end">
                <p className="m-0 max-w-[85%] whitespace-pre-wrap rounded-2xl rounded-tr-md bg-[#1769FF] px-4 py-3 text-[14px] leading-relaxed text-white shadow-[0_6px_16px_rgba(23,105,255,0.22)] sm:max-w-[75%]">{m.text}</p>
              </motion.div>
            ) : m.role === "ai" ? (
              <motion.div key={m.id} initial={{ opacity: 0, y: 6 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.3 }} className="flex justify-start">
                <div className="w-full max-w-[94%] rounded-2xl rounded-tl-md border border-slate-100 bg-white px-4 py-3 shadow-[0_1px_2px_rgba(15,23,42,0.04)] sm:max-w-[88%]">
                  <CommandCenterBlocks blocks={m.answer.blocks} historical={m.historical} />
                  <DataProvenance
                    source={m.historical ? "historical" : (m.answer.source ?? "live")}
                    dataAsOf={m.answer.dataAsOf ?? m.answer.generatedAt}
                    snapshotId={m.answer.snapshotId ?? `historical-${m.answer.generatedAt}`}
                    limitations={m.answer.limitations ?? "This saved response may not reflect current evidence or permissions."}
                    className="mt-3"
                  />
                </div>
              </motion.div>
            ) : (
              <div key={m.id} className="flex justify-start">
                <div className="max-w-[88%] rounded-2xl rounded-tl-md border border-slate-100 bg-white px-4 py-3">
                  <p className="m-0 text-[13px] text-red-700">{m.text}</p>
                  {m.requestId && m.question ? (
                    <Button variant="secondary" className="mt-3" onClick={() => void send(m.question, m.requestId)}>
                      Retry safely
                    </Button>
                  ) : null}
                </div>
              </div>
            )
          )}
          {loading && <div className="flex justify-start"><TypingDots /></div>}
          <div ref={bottomRef} />
        </div>
      </div>

      {hasContent && (
        <div className="sticky bottom-0 z-10 mx-auto w-full max-w-3xl bg-gradient-to-t from-white via-white/95 to-transparent pb-[max(0.75rem,env(safe-area-inset-bottom))] pt-6">
          <CcComposer
            value={input}
            onChange={setInput}
            onSend={() => void send()}
            disabled={loading || historyLoading || !canContinue || Boolean(historyError) || Boolean(pendingRequest)}
          />
        </div>
      )}
    </div>
  );
}
