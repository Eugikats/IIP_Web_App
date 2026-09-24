"use client";
import { useRef, useState, useEffect, useMemo, useCallback } from "react";
import { useSearchParams } from "next/navigation";
import { Sparkles, ListChecks, AlertTriangle, CheckCircle2, CircleHelp } from "lucide-react";
import { AIMessageBubble, UserMessageBubble, Composer } from "@/components/iip/ChatPrimitives";
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

// Navigation model §6.2, resolved in the browser's local timezone, which is the user's.
function timeOfDayGreeting(): string {
  const hour = new Date().getHours();
  if (hour < 12) return "Good morning";
  if (hour < 18) return "Good afternoon";
  return "Good evening";
}

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
  const restoredScrollRef = useRef(false);
  const skipDraftWriteRef = useRef(true);
  const storageKey = `iip.conversation.${requestedConversationId ?? conversationId ?? "new"}`;

  const hasContent = messages.length > 0 || input.length > 0 || loading;
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

  useEffect(() => () => {
    try { sessionStorage.setItem(`${storageKey}.scroll`, String(scrollRef.current?.scrollTop ?? 0)); } catch { /* best effort */ }
  }, [storageKey]);

  useEffect(() => {
    if (historyLoading || restoredScrollRef.current || !scrollRef.current) return;
    let saved = 0;
    try { saved = Number(sessionStorage.getItem(`${storageKey}.scroll`) ?? 0); } catch { /* best effort */ }
    scrollRef.current.scrollTo({ top: saved > 0 ? saved : scrollRef.current.scrollHeight });
    restoredScrollRef.current = true;
  }, [historyLoading, messages.length, storageKey]);

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

  // Navigation model §6.2 resolves suggestion chips in a five-step priority order. Priorities 1
  // (this user's own RequiresReview items), 2 (this user's own at-risk claims) and 4 (automation
  // escalations in the last 24h) need user-scoped or time-windowed data this screen does not
  // fetch today, useOperationsSummary is a tenant-wide aggregate, and adding a new query is out
  // of scope for a presentation-only migration. Priority 3 (tenant claims missing documents) is
  // answered by the same `incompleteClaims` count this screen already loads. See the migration
  // ledger note for this screen for the follow-up this leaves open.
  const chips = useMemo<SuggestionChip[]>(() => {
    const result: SuggestionChip[] = [];
    if (summary && summary.incompleteClaims > 0) {
      result.push({
        icon: ListChecks,
        text: "Which claims are waiting on documents?",
        question: "Which claims are waiting on documents?",
      });
    }
    if (result.length === 0) {
      result.push({ icon: Sparkles, text: "What can I ask you about?", question: "What can I ask you about?" });
    }
    return result.slice(0, 3);
  }, [summary]);

  const retryHistory = useCallback(() => setHistoryRetryKey((key) => key + 1), []);

  async function send(question?: string, retryRequestId?: string) {
    const q = (question ?? input).trim();
    if (!q || loading) return;
    const requestId = retryRequestId ?? crypto.randomUUID();
    setPendingRequest({ question: q, requestId });
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
      // Update the address without remounting. router.replace swapped to the conversation route,
      // which reloaded this conversation from history: the answer just given was relabelled as a
      // historical response with its actions disabled, and a follow-up typed during the swap was
      // sent from the page being torn down and never shown. A reload still restores it properly.
      window.history.replaceState(window.history.state, "", `/app/conversations/${answer.conversationId}`);
    }
    setMessages((current) => [...current, { id: crypto.randomUUID(), role: "ai", answer }]);
    setInput("");
    setPendingRequest(null);
  }

  // Opened from the persistent "Ask" bar available on every screen (navigation model, chat as a
  // bar, not the whole screen): a question typed there arrives here as ?q=... and is sent once,
  // then the query string is cleared so a refresh does not resend it.
  useEffect(() => {
    const question = searchParams.get("q");
    if (!question || autoSendRef.current || requestedConversationId) return;
    autoSendRef.current = true;
    window.history.replaceState(window.history.state, "", window.location.pathname);
    void send(question);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchParams, requestedConversationId]);

  return (
    <div className="flex min-h-[calc(100dvh-8rem)] flex-col">
      {/* Navigation model §6.2, exactly four elements, vertically centred, nothing else. */}
      {!hasContent && (
        <div className="mx-auto flex w-full max-w-[520px] flex-1 flex-col items-center justify-center gap-5 py-10 text-center">
          <div className="dls-orb-float h-16 w-16 rounded-full bg-dls-orb" aria-hidden="true" />
          <div>
            <h1 className="text-[26px] font-extrabold leading-tight text-dls-text-primary">
              {givenName ? `${timeOfDayGreeting()}, ${givenName},` : `${timeOfDayGreeting()},`}
            </h1>
            <p className="mt-1 text-[15px] text-dls-text-secondary">How can I help with your work today?</p>
          </div>
          <div className="flex flex-wrap justify-center gap-2">
            {chips.map((chip) => (
              <button
                key={chip.text}
                type="button"
                onClick={() => send(chip.question)}
                className="dls-focus-outline inline-flex items-center gap-2 rounded-dls-chip border border-dls-border-input px-3.5 py-2.5 text-[13.5px] font-semibold text-dls-text-primary transition-colors duration-dls-fast ease-dls hover:border-dls-primary"
              >
                <chip.icon className="h-[15px] w-[15px] text-dls-primary" aria-hidden="true" />
                {chip.text}
              </button>
            ))}
          </div>
        </div>
      )}

      <div ref={scrollRef} className={hasContent ? "flex-1 overflow-y-auto" : "hidden"}>
        <div className="mx-auto w-full max-w-[780px] space-y-6 pb-6">
          {historyLoading && <LoadingSkeleton rows={2} />}
          {historyError && <ErrorStateSurface message={historyError} onRetry={retryHistory} />}
          {requestedConversationId && !historyLoading && !historyError && (
            <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface px-4 py-3 text-[12.5px] text-dls-text-secondary">
              Historical responses are shown for continuity. IIP checks current permissions and live workspace data again when you continue.
            </div>
          )}
          {grounding && !historyLoading && !historyError ? (
            <div
              role={grounding.status === "changed" ? "alert" : "status"}
              className={
                "rounded-dls-card border px-4 py-3 text-[12.5px] " +
                (grounding.status === "changed"
                  ? "border-dls-border-subtle bg-dls-warning-tint text-dls-warning-text"
                  : "border-dls-border-subtle bg-dls-surface text-dls-text-secondary")
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
              <div className="mt-1 text-[10.5px] text-dls-text-muted">
                Saved {new Date(grounding.historicalTimestamp).toLocaleString()}
              </div>
            </div>
          ) : null}
          {messages.map((m) =>
            m.role === "user" ? (
              <UserMessageBubble key={m.id} text={m.text} />
            ) : m.role === "ai" ? (
              <AIMessageBubble key={m.id}>
                <CommandCenterBlocks blocks={m.answer.blocks} historical={m.historical} />
                <DataProvenance
                  source={m.historical ? "historical" : (m.answer.source ?? "live")}
                  dataAsOf={m.answer.dataAsOf ?? m.answer.generatedAt}
                  snapshotId={m.answer.snapshotId ?? `historical-${m.answer.generatedAt}`}
                  limitations={m.answer.limitations ?? "This saved response may not reflect current evidence or permissions."}
                  className="mt-3"
                />
              </AIMessageBubble>
            ) : (
              <AIMessageBubble key={m.id}>
                <p className="text-[13px] text-dls-danger-text">{m.text}</p>
                {m.requestId && m.question ? (
                  <Button variant="secondary" className="mt-3" onClick={() => void send(m.question, m.requestId)}>
                    Retry safely
                  </Button>
                ) : null}
              </AIMessageBubble>
            )
          )}
          {loading && (
            <div className="flex items-center gap-2 text-[12.5px] text-dls-primary">
              <span className="h-2 w-2 rounded-full bg-dls-primary" aria-hidden="true" /> IIP is preparing a grounded response&hellip;
            </div>
          )}
        </div>
      </div>

      <div className="sticky bottom-0 z-10 mx-auto w-full max-w-[780px] bg-dls-bg-page pt-4 pb-[max(0.5rem,env(safe-area-inset-bottom))]">
        <Composer
          value={input}
          onChange={setInput}
          onSend={() => send()}
          disabled={loading || historyLoading || !canContinue || Boolean(historyError) || Boolean(pendingRequest)}
          disclaimerClassName="text-dls-text-secondary"
        />
      </div>
    </div>
  );
}
