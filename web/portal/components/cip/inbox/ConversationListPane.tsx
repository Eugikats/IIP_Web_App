"use client";

// Ported from the CIP web app (WhatsApp inbox, ConversationListPane): search, a filter button, count
// tabs and rows with a status dot, avatar, name, time and preview. The tabs and filters are the ones
// that make sense here: who needs attention, and who is answering (Kato or a person).

import { useEffect, useMemo, useRef, useState } from "react";
import { ChevronRight, Search, SlidersHorizontal } from "lucide-react";
import type { ChannelConversation } from "@/lib/lovable-parity/mock";
import type { WhatsAppHealthStatus } from "@/lib/whatsappHealth";

type ListTab = "all" | "attention" | "person" | "kato";

export function conversationInitials(name: string): string {
  if (/^whatsapp contact/i.test(name)) return "WA";
  const parts = name.trim().split(/\s+/).filter(Boolean);
  return parts.length === 0 ? "?" : parts.slice(0, 2).map((part) => part[0]!.toUpperCase()).join("");
}

function needsAttention(conversation: ChannelConversation) {
  return conversation.needsApproval || conversation.intent === "Wants a person";
}

function matchesTab(conversation: ChannelConversation, tab: ListTab) {
  if (tab === "all") return true;
  if (tab === "attention") return needsAttention(conversation);
  if (tab === "person") return conversation.controlMode === "Person";
  return conversation.channel === "WhatsApp" && conversation.controlMode !== "Person";
}

function dotClass(conversation: ChannelConversation) {
  if (needsAttention(conversation)) return "bg-amber-500";
  if (conversation.controlMode === "Person") return "bg-[#1769FF]";
  if (conversation.controlMode === "Kato") return "bg-emerald-500";
  return "bg-slate-300";
}

export default function ConversationListPane({
  conversations,
  selectedId,
  loading,
  error,
  whatsAppStatus,
  onSelect,
}: {
  conversations: ChannelConversation[];
  selectedId: string | null;
  loading: boolean;
  error?: string | null;
  whatsAppStatus: WhatsAppHealthStatus;
  onSelect: (id: string) => void;
}) {
  const [search, setSearch] = useState("");
  const [tab, setTab] = useState<ListTab>("all");
  const [channel, setChannel] = useState("all");
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (!menuOpen) return;
    const onDown = (event: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(event.target as Node)) setMenuOpen(false);
    };
    document.addEventListener("mousedown", onDown);
    return () => document.removeEventListener("mousedown", onDown);
  }, [menuOpen]);

  const channels = useMemo(() => [...new Set(conversations.map((conversation) => conversation.channel))].sort(), [conversations]);
  const inChannel = useMemo(() => conversations.filter((conversation) => channel === "all" || conversation.channel === channel), [conversations, channel]);

  const counts = {
    all: inChannel.length,
    attention: inChannel.filter((c) => matchesTab(c, "attention")).length,
    person: inChannel.filter((c) => matchesTab(c, "person")).length,
    kato: inChannel.filter((c) => matchesTab(c, "kato")).length,
  };

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return inChannel.filter((conversation) => {
      if (!matchesTab(conversation, tab)) return false;
      if (!q) return true;
      return `${conversation.customer} ${conversation.customerContact ?? ""} ${conversation.snippet}`.toLowerCase().includes(q);
    });
  }, [inChannel, search, tab]);

  const tabs: { id: ListTab; label: string }[] = [
    { id: "all", label: "All" },
    { id: "attention", label: "Attention" },
    { id: "person", label: "Person" },
    { id: "kato", label: "Kato" },
  ];

  return (
    <div className="flex h-full min-h-0 flex-col border-r border-[#E6EAF2] bg-white">
      <div className="space-y-3 border-b border-[#E6EAF2] px-3 py-3">
        <div className="relative flex items-center gap-2">
          <div className="relative min-w-0 flex-1">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-slate-400" />
            <input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search conversations..."
              aria-label="Search conversations"
              className="w-full rounded-xl border border-[#E6EAF2] bg-[#F7F9FC] py-2 pl-9 pr-3 text-[12px] text-[#0B1640] outline-none placeholder:text-slate-400 focus:border-[#1769FF] focus:ring-2 focus:ring-[#1769FF]/15"
            />
          </div>
          <div className="relative" ref={menuRef}>
            <button
              type="button"
              onClick={() => setMenuOpen((open) => !open)}
              aria-label="Filter by channel"
              aria-expanded={menuOpen}
              className={`inline-flex h-9 w-9 items-center justify-center rounded-xl border ${
                channel !== "all" ? "border-[#1769FF] bg-[#EEF4FF] text-[#1769FF]" : "border-[#E6EAF2] bg-white text-slate-500 hover:bg-slate-50"
              }`}
            >
              <SlidersHorizontal className="h-3.5 w-3.5" />
            </button>
            {menuOpen ? (
              <div className="absolute right-0 z-20 mt-1 w-44 overflow-hidden rounded-xl border border-[#E6EAF2] bg-white py-1 shadow-lg">
                {["all", ...channels].map((option) => (
                  <button
                    key={option}
                    type="button"
                    onClick={() => { setChannel(option); setMenuOpen(false); }}
                    className={`flex w-full items-center px-3 py-2 text-left text-[11px] ${
                      channel === option ? "bg-[#EEF4FF] font-semibold text-[#1769FF]" : "text-slate-600 hover:bg-slate-50"
                    }`}
                  >
                    {option === "all" ? "All channels" : option}
                  </button>
                ))}
              </div>
            ) : null}
          </div>
        </div>

        <div className="flex flex-wrap gap-x-1 gap-y-0.5" role="tablist" aria-label="Conversation views">
          {tabs.map((item) => (
            <button
              key={item.id}
              type="button"
              role="tab"
              aria-selected={tab === item.id}
              onClick={() => setTab(item.id)}
              className={`shrink-0 whitespace-nowrap border-b-2 px-2 pb-2 text-[11px] font-semibold transition-colors ${
                tab === item.id ? "border-[#1769FF] text-[#1769FF]" : "border-transparent text-slate-500 hover:text-[#0B1640]"
              }`}
            >
              {item.label} ({counts[item.id]})
            </button>
          ))}
        </div>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto">
        {loading && conversations.length === 0 ? (
          <div className="space-y-2 p-3">
            {[...Array(6)].map((_, index) => <div key={index} className="h-14 animate-pulse rounded-xl bg-[#F7F9FC]" />)}
          </div>
        ) : error ? (
          <p className="p-4 text-[12px] text-rose-600">{error}</p>
        ) : conversations.length === 0 ? (
          <p className="p-4 text-[12px] leading-relaxed text-slate-500">{whatsAppStatus === "not_configured"
            ? "WhatsApp is not configured for this workspace. Connect its business number in Integrations to receive those messages here."
            : "Conversations appear here as soon as someone writes to a channel connected to this workspace."}</p>
        ) : filtered.length === 0 ? (
          <p className="p-4 text-[12px] text-slate-500">No conversations match this view.</p>
        ) : (
          filtered.map((conversation) => {
            const active = selectedId === conversation.id;
            const attention = needsAttention(conversation);
            return (
              <button
                key={conversation.id}
                type="button"
                onClick={() => onSelect(conversation.id)}
                aria-current={active ? "true" : undefined}
                className={`flex w-full items-center gap-2.5 border-b border-[#F0F3F8] px-3 py-3 text-left transition-colors hover:bg-[#F7F9FC] ${active ? "bg-[#EEF4FF]" : ""}`}
              >
                <span className={`h-2 w-2 shrink-0 rounded-full ${dotClass(conversation)}`} aria-hidden />
                <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-[#EEF4FF] text-[11px] font-bold text-[#1769FF]">
                  {conversationInitials(conversation.customer)}
                </div>
                <div className="min-w-0 flex-1">
                  <div className="flex items-center justify-between gap-2">
                    <span className={`truncate text-[13px] text-[#0B1640] ${attention ? "font-bold" : "font-semibold"}`}>{conversation.customer}</span>
                    <span className="shrink-0 text-[10px] text-slate-400">{conversation.updated}</span>
                  </div>
                  <p className="m-0 mt-0.5 truncate text-[12px] text-slate-500">{conversation.snippet || "No messages"}</p>
                  <div className="mt-1 flex flex-wrap items-center gap-1">
                    {/^whatsapp contact/i.test(conversation.customer) ? <span className="rounded-full border border-slate-200 bg-slate-50 px-1.5 py-0.5 text-[9.5px] font-semibold text-slate-500">New contact</span> : null}
                    {conversation.controlMode === "Person" ? <span className="rounded-full border border-sky-100 bg-sky-50 px-1.5 py-0.5 text-[9.5px] font-semibold text-sky-700">With a person</span> : null}
                    {attention ? <span className="rounded-full border border-amber-100 bg-amber-50 px-1.5 py-0.5 text-[9.5px] font-semibold text-amber-700">{conversation.intent === "Wants a person" ? "Wants a person" : "Needs approval"}</span> : null}
                    {conversation.language ? <span className="rounded-full border border-blue-100 bg-blue-50 px-1.5 py-0.5 text-[9.5px] font-semibold text-blue-700">{conversation.language}</span> : null}
                  </div>
                </div>
                <ChevronRight className="h-3.5 w-3.5 shrink-0 text-slate-300 md:hidden" />
              </button>
            );
          })
        )}
      </div>
    </div>
  );
}
