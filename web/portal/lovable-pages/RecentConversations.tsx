"use client";

import Link from "next/link";
import { Archive, Clock3, Loader2, MessageSquareText, RotateCcw } from "lucide-react";
import { useCallback, useEffect, useState } from "react";
import { GlassCard, SectionHeader } from "@/components/iip/Primitives";
import { useUI } from "@/context/UIStateContext";
import { archiveConversation, listConversations, restoreConversation, type ConversationSummary } from "@/lib/lovable-parity/data/commandCenterConversations";
import { useRuntimeContext } from "@/lib/runtimeContext";

function when(value: string) {
  const date = new Date(value);
  return Number.isNaN(date.valueOf()) ? "Activity time unavailable" : date.toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" });
}

export default function RecentConversations() {
  const { roleDef } = useUI();
  const { runtimeContext, loading: runtimeLoading } = useRuntimeContext();
  const [archived, setArchived] = useState(false);
  const [items, setItems] = useState<ConversationSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [changingId, setChangingId] = useState<string | null>(null);

  const load = useCallback((signal?: AbortSignal) => {
    if (!runtimeContext) {
      setLoading(runtimeLoading);
      setError(runtimeLoading ? null : "Current workspace identity could not be resolved. Refresh after signing in again.");
      return Promise.resolve();
    }
    setLoading(true);
    setError(null);
    return listConversations(roleDef.key, runtimeContext.userId, archived, 100, signal)
      .then((result) => setItems(result.items))
      .catch((reason: unknown) => {
        if (reason instanceof DOMException && reason.name === "AbortError") return;
        setError(reason instanceof Error ? reason.message : "Conversations could not be loaded.");
      })
      .finally(() => { if (!signal?.aborted) setLoading(false); });
  }, [archived, roleDef.key, runtimeContext, runtimeLoading]);

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [load]);

  async function change(item: ConversationSummary) {
    if (!runtimeContext) return;
    setChangingId(item.conversationId);
    setError(null);
    try {
      if (archived) await restoreConversation(item, roleDef.key, runtimeContext.userId);
      else await archiveConversation(item, roleDef.key, runtimeContext.userId);
      setItems((current) => current.filter((candidate) => candidate.conversationId !== item.conversationId));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Conversation could not be updated.");
    } finally {
      setChangingId(null);
    }
  }

  return (
    <>
      <SectionHeader eyebrow="Command Center continuity" title="All conversations" sub="Only your conversations in the current workspace are shown. Historical answers are checked against current authority and data when continued." />
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <div className="inline-flex rounded-xl border border-cloud bg-white/70 p-1" aria-label="Conversation status">
          <button onClick={() => setArchived(false)} aria-pressed={!archived} className={`rounded-lg px-3 py-1.5 text-[12px] ${!archived ? "bg-navy text-white" : "text-slate2"}`}>Active</button>
          <button onClick={() => setArchived(true)} aria-pressed={archived} className={`rounded-lg px-3 py-1.5 text-[12px] ${archived ? "bg-navy text-white" : "text-slate2"}`}>Archived</button>
        </div>
        <Link href="/app" className="rounded-xl bg-primary px-4 py-2 text-[12.5px] font-medium text-white focus-ring">New conversation</Link>
      </div>

      <div aria-live="polite" className="sr-only">{loading ? "Loading conversations" : error ?? `${items.length} conversations`}</div>
      {loading ? (
        <GlassCard><div className="flex items-center justify-center gap-2 py-12 text-sm text-stone"><Loader2 className="h-4 w-4 animate-spin" />Loading conversations…</div></GlassCard>
      ) : error ? (
        <GlassCard><div className="py-10 text-center"><div className="font-medium text-navy">Conversations unavailable</div><p className="mt-1 text-sm text-stone">{error}</p><button onClick={() => void load()} className="mt-4 rounded-xl border border-cloud px-4 py-2 text-sm text-navy focus-ring">Retry</button></div></GlassCard>
      ) : items.length === 0 ? (
        <GlassCard><div className="py-12 text-center"><MessageSquareText className="mx-auto h-7 w-7 text-stone" /><div className="mt-3 font-medium text-navy">{archived ? "No archived conversations" : "No conversations yet"}</div><p className="mt-1 text-sm text-stone">{archived ? "Archived work will appear here." : "Start in the Command Center to investigate or prepare insurance work."}</p></div></GlassCard>
      ) : (
        <GlassCard className="!p-0 overflow-hidden">
          <ul>
            {items.map((item) => (
              <li key={item.conversationId} className="flex items-center gap-3 border-t border-cloud/70 p-4 first:border-t-0">
                <Link href={`/app/conversations/${item.conversationId}`} className="min-w-0 flex-1 rounded-lg focus-ring">
                  <span className="block truncate text-[13px] font-medium text-navy">{item.title}</span>
                  <span className="mt-1 flex flex-wrap items-center gap-2 text-[11.5px] text-stone"><Clock3 className="h-3.5 w-3.5" />{when(item.lastActivityAt)} · {item.turnCount} turn{item.turnCount === 1 ? "" : "s"}</span>
                </Link>
                <button onClick={() => void change(item)} disabled={changingId === item.conversationId} className="inline-flex items-center gap-1.5 rounded-xl border border-cloud px-3 py-2 text-[11.5px] text-slate2 hover:bg-white focus-ring disabled:opacity-50" aria-label={`${archived ? "Restore" : "Archive"} ${item.title}`}>
                  {archived ? <RotateCcw className="h-3.5 w-3.5" /> : <Archive className="h-3.5 w-3.5" />}{archived ? "Restore" : "Archive"}
                </button>
              </li>
            ))}
          </ul>
        </GlassCard>
      )}
    </>
  );
}
