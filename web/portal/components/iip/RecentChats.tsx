"use client";

import { useEffect, useState } from "react";
import { MessageSquarePlus } from "lucide-react";
import { Link, useLocation } from "@/components/iip/NextRouterCompat";
import { listConversations, type ConversationSummary } from "@/lib/lovable-parity/data/commandCenterConversations";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { useRuntimeContext } from "@/lib/runtimeContext";
import { useUI } from "@/context/UIStateContext";
import { cn } from "@/lib/utils";

const VISIBLE = 6;

function relative(value: string): string {
  const then = new Date(value).getTime();
  if (Number.isNaN(then)) return "";
  const minutes = Math.max(0, Math.round((Date.now() - then) / 60000));
  if (minutes < 1) return "now";
  if (minutes < 60) return `${minutes}m`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours}h`;
  return `${Math.round(hours / 24)}d`;
}

/** The sidebar's list of recent Command Center chats, as in CIP. Shown only while the sidebar is open. */
export default function RecentChats({ expanded, onNavigate }: { expanded: boolean; onNavigate?: () => void }) {
  const { roleDef } = useUI();
  const { runtimeContext } = useRuntimeContext();
  const loc = useLocation();
  const [items, setItems] = useState<ConversationSummary[] | null>(null);
  const [failed, setFailed] = useState(false);
  const [showAll, setShowAll] = useState(false);
  const userId = runtimeContext?.userId;

  useEffect(() => {
    if (LOVABLE_VISUAL_PARITY_MODE || !userId) {
      setItems([]);
      return;
    }
    const controller = new AbortController();
    setFailed(false);
    void listConversations(roleDef.key, userId, false, 20, controller.signal)
      .then((list) => setItems(list.items))
      .catch((reason: unknown) => {
        if (reason instanceof DOMException && reason.name === "AbortError") return;
        setFailed(true);
        setItems([]);
      });
    return () => controller.abort();
    // Reloaded when the person moves between chats, so a new one appears in the list.
  }, [roleDef.key, userId, loc.pathname]);

  if (!expanded) {
    return (
      <div className="mt-2 flex justify-center border-t border-dls-border-subtle pt-2">
        <Link
          to="/app"
          onClick={onNavigate}
          title="New chat"
          aria-label="New chat"
          className="dls-focus-outline inline-flex h-8 w-8 items-center justify-center rounded-md text-[#1769FF] transition-colors hover:bg-blue-50"
        >
          <MessageSquarePlus className="h-4 w-4" aria-hidden="true" />
        </Link>
      </div>
    );
  }

  const shown = showAll ? items ?? [] : (items ?? []).slice(0, VISIBLE);
  const hidden = (items?.length ?? 0) - shown.length;

  return (
    <div className="mt-3 flex min-h-0 flex-col border-t border-dls-border-subtle pt-3">
      <div className="mb-1.5 flex h-6 items-center justify-between px-2.5">
        <span className="text-[10px] font-semibold uppercase tracking-wide text-slate-400">Recent chats</span>
        <Link
          to="/app"
          onClick={onNavigate}
          title="New chat"
          aria-label="New chat"
          className="dls-focus-outline inline-flex items-center gap-1 rounded-md px-1.5 py-0.5 text-[10px] font-medium text-[#1769FF] transition-colors hover:bg-blue-50"
        >
          <MessageSquarePlus className="h-3.5 w-3.5" aria-hidden="true" />
          New
        </Link>
      </div>
      {items === null ? (
        <p className="m-0 px-2.5 text-xs text-slate-400">Loading chats...</p>
      ) : failed ? (
        <p className="m-0 px-2.5 text-xs text-slate-400">Chats could not be loaded.</p>
      ) : items.length === 0 ? (
        <p className="m-0 px-2.5 text-xs text-slate-400">No conversations yet</p>
      ) : (
        <div className="min-h-0 space-y-0.5 overflow-y-auto">
          {shown.map((conversation) => {
            const active = loc.pathname === `/app/conversations/${conversation.conversationId}`;
            return (
              <Link
                key={conversation.conversationId}
                to={`/app/conversations/${conversation.conversationId}`}
                onClick={onNavigate}
                className={cn(
                  "dls-focus-outline flex h-[42px] w-full flex-col justify-center rounded-md px-2.5 text-left transition-colors",
                  active ? "bg-[#eff6ff] text-[#1d4ed8]" : "text-slate-600 hover:bg-slate-100 hover:text-slate-900",
                )}
              >
                <span className="truncate text-[13px] font-medium">{conversation.title}</span>
                <span className="mt-0.5 flex items-center justify-between gap-2 text-[10px] text-slate-400">
                  <span className="truncate">{conversation.turnCount} {conversation.turnCount === 1 ? "question" : "questions"}</span>
                  <span className="shrink-0">{relative(conversation.lastActivityAt)}</span>
                </span>
              </Link>
            );
          })}
          {hidden > 0 || showAll ? (
            <button
              type="button"
              onClick={() => setShowAll((value) => !value)}
              className="dls-focus-outline mt-1 flex h-7 items-center gap-1.5 rounded-md px-2.5 text-xs font-medium text-[#1769FF] transition-colors hover:bg-blue-50"
            >
              {showAll ? "Show less" : `View more (${hidden})`}
            </button>
          ) : null}
        </div>
      )}
    </div>
  );
}
