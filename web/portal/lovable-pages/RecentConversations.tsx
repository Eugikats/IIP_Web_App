"use client";

import Link from "next/link";
import { Archive, Clock3, RotateCcw, Search } from "lucide-react";
import { useCallback, useEffect, useState } from "react";
import { GlassCard, SectionHeader } from "@/components/iip/Primitives";
import { Input } from "@/components/ui/Input";
import { FilterTab } from "@/components/ui/FilterTab";
import { LoadingSkeleton, ErrorStateSurface, EmptyStateSurface, EmptyFilteredStateSurface } from "@/components/ui/StateSurfaces";
import { useUI } from "@/context/UIStateContext";
import { archiveConversation, ConversationRequestError, listConversations, restoreConversation, type ConversationSummary } from "@/lib/lovable-parity/data/commandCenterConversations";
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
  const [error, setError] = useState<{ kind: "unauthorized" | "unavailable"; message: string } | null>(null);
  // Distinct from `error` (the whole list failed to load): a single row's archive/restore action
  // failing must not blank the list out from under every other row — it shows inline instead.
  const [actionError, setActionError] = useState<string | null>(null);
  const [changingId, setChangingId] = useState<string | null>(null);
  const [query, setQuery] = useState("");

  useEffect(() => {
    try {
      setArchived(sessionStorage.getItem("iip.conversations.archived") === "true");
      setQuery(sessionStorage.getItem("iip.conversations.query") ?? "");
      window.scrollTo({ top: Number(sessionStorage.getItem("iip.conversations.scroll") ?? 0) });
    } catch { /* Storage may be unavailable in a restricted browser. */ }
    return () => {
      try { sessionStorage.setItem("iip.conversations.scroll", String(window.scrollY)); } catch { /* best effort */ }
    };
  }, []);

  useEffect(() => {
    try {
      sessionStorage.setItem("iip.conversations.archived", String(archived));
      sessionStorage.setItem("iip.conversations.query", query);
    } catch { /* best effort */ }
  }, [archived, query]);

  const load = useCallback((signal?: AbortSignal) => {
    if (!runtimeContext) {
      setLoading(runtimeLoading);
      setError(runtimeLoading ? null : { kind: "unauthorized", message: "Current workspace identity could not be resolved. Refresh after signing in again." });
      return Promise.resolve();
    }
    setLoading(true);
    setError(null);
    setActionError(null);
    return listConversations(roleDef.key, runtimeContext.userId, archived, 100, signal)
      .then((result) => setItems(result.items))
      .catch((reason: unknown) => {
        if (reason instanceof DOMException && reason.name === "AbortError") return;
        setError({
          kind: reason instanceof ConversationRequestError && reason.kind === "unauthorized" ? "unauthorized" : "unavailable",
          message: reason instanceof Error ? reason.message : "Conversations could not be loaded.",
        });
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
    setActionError(null);
    try {
      if (archived) await restoreConversation(item, roleDef.key, runtimeContext.userId);
      else await archiveConversation(item, roleDef.key, runtimeContext.userId);
      setItems((current) => current.filter((candidate) => candidate.conversationId !== item.conversationId));
    } catch (reason) {
      setActionError(reason instanceof Error ? reason.message : "Conversation could not be updated.");
    } finally {
      setChangingId(null);
    }
  }

  const normalizedQuery = query.trim().toLowerCase();
  const filteredItems = normalizedQuery
    ? items.filter((item) => [item.title, item.primaryFocus?.label, item.ownerUserId].some((value) => value?.toLowerCase().includes(normalizedQuery)))
    : items;

  return (
    <>
      <SectionHeader eyebrow="Command Center continuity" title="All conversations" sub="Only your conversations in the current workspace are shown. Historical answers are checked against current authority and data when continued." />
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap gap-1.5" aria-label="Conversation status">
          <FilterTab active={!archived} onClick={() => setArchived(false)}>Active</FilterTab>
          <FilterTab active={archived} onClick={() => setArchived(true)}>Archived</FilterTab>
        </div>
        <Link
          href="/app"
          className="dls-focus-outline inline-flex min-h-11 items-center justify-center gap-1.5 rounded-dls-control bg-dls-primary px-4 py-[9px] text-[13px] font-bold text-dls-text-inverse shadow-dls-primary transition-colors duration-dls-fast ease-dls hover:bg-dls-primary-hover"
        >
          New conversation
        </Link>
      </div>

      <Input
        value={query}
        onChange={(event) => setQuery(event.target.value)}
        placeholder="Find by title, claim, work item, or owner"
        leadingIcon={<Search aria-hidden="true" />}
        aria-label="Find conversations"
        className="mb-4 max-w-xl"
      />

      <div aria-live="polite" className="sr-only">{loading ? "Loading conversations" : error?.message ?? `${items.length} conversations`}</div>
      {loading ? (
        <GlassCard><LoadingSkeleton rows={4} /></GlassCard>
      ) : error ? (
        <GlassCard>
          <ErrorStateSurface
            title={error.kind === "unauthorized" ? "Conversation access unavailable" : "Conversations unavailable"}
            message={error.message}
            onRetry={() => void load()}
          />
        </GlassCard>
      ) : items.length === 0 ? (
        <GlassCard>
          <EmptyStateSurface message={archived ? "Archived work will appear here." : "Start in the Command Center to investigate or prepare insurance work."} />
        </GlassCard>
      ) : filteredItems.length === 0 ? (
        <GlassCard>
          <EmptyFilteredStateSurface onClear={() => setQuery("")} />
        </GlassCard>
      ) : (
        <>
          {actionError ? (
            <div role="alert" className="mb-3 rounded-dls-control bg-dls-warning-tint px-4 py-2.5 text-[12.5px] text-dls-warning-text">
              <p>{actionError}</p>
            </div>
          ) : null}
          <GlassCard className="!p-0 overflow-hidden">
            <ul>
              {filteredItems.map((item) => (
                <li key={item.conversationId} className="flex items-center gap-3 border-t border-dls-border-subtle p-4 first:border-t-0">
                  <Link href={`/app/conversations/${item.conversationId}`} className="dls-focus-outline min-w-0 flex-1 rounded-dls-control">
                    <span className="block truncate text-[13px] font-semibold text-dls-text-primary">{item.title}</span>
                    <span className="mt-1 flex flex-wrap items-center gap-2 text-[11.5px] text-dls-text-muted">
                      <Clock3 className="h-3.5 w-3.5" aria-hidden="true" />
                      {when(item.lastActivityAt)} · {item.turnCount} turn{item.turnCount === 1 ? "" : "s"}
                    </span>
                    <span className="mt-1 block text-[11.5px] text-dls-text-secondary">
                      {item.primaryFocus?.label ?? "Workspace operations"} · Owner: {item.ownerUserId === runtimeContext?.userId ? "You" : item.ownerUserId} · {item.status === "archived" ? "Archived" : "Active"}
                    </span>
                    <span className={`mt-1 block text-[11px] ${item.grounding.status === "changed" ? "font-semibold text-dls-warning-text" : "text-dls-text-muted"}`}>
                      {item.grounding.status === "current" ? "Evidence current at last check" : item.grounding.status === "changed" ? "Evidence changed since saved answer" : "Current evidence not checked"}
                    </span>
                  </Link>
                  <button
                    type="button"
                    onClick={() => void change(item)}
                    disabled={changingId === item.conversationId}
                    className="dls-focus-outline inline-flex items-center gap-1.5 rounded-dls-control border border-dls-border-input px-3 py-2 text-[11.5px] font-semibold text-dls-text-secondary transition-colors duration-dls-fast ease-dls hover:bg-dls-bg-page disabled:opacity-60"
                    aria-label={`${archived ? "Restore" : "Archive"} ${item.title}`}
                  >
                    {archived ? <RotateCcw className="h-3.5 w-3.5" aria-hidden="true" /> : <Archive className="h-3.5 w-3.5" aria-hidden="true" />}
                    {archived ? "Restore" : "Archive"}
                  </button>
                </li>
              ))}
            </ul>
          </GlassCard>
        </>
      )}
    </>
  );
}
