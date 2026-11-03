"use client";

// Ported from the CIP web app (WhatsApp inbox, ChatThreadPane, ChatBubble, ChatComposer): header with
// who is answering, date separators, white customer bubbles on the left, Kato in light blue and a
// person in solid blue on the right, and a rounded composer with a round send button. Control of the
// conversation is explicit: Kato answers until a person takes over, and Kato takes it back when the
// person hands it over or goes quiet.

import Link from "next/link";
import { useEffect, useMemo, useRef, useState } from "react";
import { Bot, ChevronLeft, Info, Loader2, Paperclip, RefreshCw, Send, UserRound } from "lucide-react";
import { toast } from "sonner";
import type { ChannelConversation } from "@/lib/lovable-parity/mock";
import type { ChannelMessage, ConversationControl } from "@/lib/lovable-parity/data/channels";
import { describePerson, usePeople } from "@/lib/people";
import { conversationInitials } from "./ConversationListPane";

const TEXT_LIMIT = 4096;

type Sender = "customer" | "kato" | "person";

function senderOf(message: ChannelMessage): Sender {
  if (message.direction === "Inbound") return "customer";
  return (message.sentByUserId ?? "").toLowerCase().startsWith("system:") || !message.sentByUserId ? "kato" : "person";
}

function dayLabel(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return "";
  const today = new Date();
  const yesterday = new Date();
  yesterday.setDate(today.getDate() - 1);
  if (date.toDateString() === today.toDateString()) return "Today";
  if (date.toDateString() === yesterday.toDateString()) return "Yesterday";
  return date.toLocaleDateString("en-GB", { day: "numeric", month: "long", year: "numeric" });
}

function timeLabel(iso: string): string {
  const date = new Date(iso);
  return Number.isNaN(date.getTime()) ? "" : date.toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit" });
}

function deliveryLabel(state: ChannelMessage["deliveryState"]): { text: string; failed: boolean } {
  switch (state) {
    case "Read": return { text: "Read", failed: false };
    case "Delivered": return { text: "Delivered", failed: false };
    case "ProviderAccepted": return { text: "Sent", failed: false };
    case "Queued": return { text: "Sending", failed: false };
    case "Failed": return { text: "Not delivered", failed: true };
    case "Unknown": return { text: "Not confirmed", failed: true };
    default: return { text: "", failed: false };
  }
}

function Bubble({ message, personName }: { message: ChannelMessage; personName: string }) {
  const sender = senderOf(message);
  const customer = sender === "customer";
  const status = !customer ? deliveryLabel(message.deliveryState) : null;
  return (
    <div className={`flex ${customer ? "justify-start" : "justify-end"}`}>
      <div className={`max-w-[82%] ${customer ? "" : "text-right"}`}>
        {!customer ? (
          <p className="m-0 mb-1 text-[10px] font-semibold uppercase tracking-wide text-slate-400">{sender === "person" ? personName : "Kato"}</p>
        ) : null}
        <div
          className={
            customer
              ? "whitespace-pre-wrap rounded-2xl rounded-tl-sm border border-[#E6EAF2] bg-white px-3 py-2 text-left text-[14px] leading-relaxed text-[#0B1640] shadow-sm"
              : sender === "person"
                ? "whitespace-pre-wrap rounded-2xl rounded-tr-sm bg-[#1769FF] px-3 py-2 text-left text-[14px] leading-relaxed text-white shadow-sm"
                : "whitespace-pre-wrap rounded-2xl rounded-tr-sm bg-[#EEF4FF] px-3 py-2 text-left text-[14px] leading-relaxed text-[#0B1640] shadow-sm"
          }
        >
          {message.body}
        </div>
        <div className={`mt-1 flex items-center gap-2 text-[10px] text-slate-400 ${customer ? "" : "justify-end"}`}>
          <span>{timeLabel(message.createdAt)}</span>
          {status && status.text ? <span className={status.failed ? "text-rose-600" : "text-slate-500"}>{status.text}</span> : null}
        </div>
      </div>
    </div>
  );
}

export default function ChatThreadPane({
  conversation,
  messages,
  pendingReplyDraft,
  canReplyInInbox,
  loading,
  error,
  onRefresh,
  control,
  controlSaving,
  controlError,
  onTakeOver,
  onHandBack,
  onSend,
  onBack,
  onOpenInfo,
}: {
  conversation: ChannelConversation;
  messages: ChannelMessage[];
  pendingReplyDraft?: string | null;
  canReplyInInbox: boolean;
  loading: boolean;
  error: string | null;
  onRefresh: () => void;
  control: ConversationControl | null;
  controlSaving: boolean;
  controlError: string | null;
  onTakeOver: () => Promise<boolean>;
  onHandBack: () => Promise<boolean>;
  onSend: (input: { body: string; templateName?: string; caseId?: string | null; language: string }) => Promise<void>;
  onBack?: () => void;
  onOpenInfo?: () => void;
}) {
  const { people, currentUserId } = usePeople();
  const isWhatsApp = conversation.channel === "WhatsApp";
  const isTelegram = conversation.channel === "Telegram";
  const canUseComposer = (isWhatsApp || isTelegram) && canReplyInInbox;
  const withPerson = (control?.mode ?? conversation.controlMode ?? "Kato") === "Person";
  const bottomRef = useRef<HTMLDivElement | null>(null);
  const textareaRef = useRef<HTMLTextAreaElement | null>(null);
  const [text, setText] = useState("");
  const [sending, setSending] = useState(false);
  const [sendError, setSendError] = useState<string | null>(null);
  const [needsTemplate, setNeedsTemplate] = useState(false);
  const [templateName, setTemplateName] = useState("");

  const linkedCaseId = messages.find((message) => message.caseId)?.caseId ?? null;
  const language = "en";

  const grouped = useMemo(() => {
    const groups: { day: string; items: ChannelMessage[] }[] = [];
    for (const message of messages) {
      const day = dayLabel(message.createdAt);
      const last = groups[groups.length - 1];
      if (last && last.day === day) last.items.push(message);
      else groups.push({ day, items: [message] });
    }
    return groups;
  }, [messages]);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ block: "end" });
  }, [messages.length, conversation.id]);

  useEffect(() => {
    setText("");
    setSendError(null);
    setNeedsTemplate(false);
    setTemplateName("");
  }, [conversation.id]);

  useEffect(() => {
    const el = textareaRef.current;
    if (!el) return;
    el.style.height = "auto";
    el.style.height = `${Math.min(el.scrollHeight, 180)}px`;
  }, [text]);

  function usePendingDraft() {
    if (!pendingReplyDraft) return;
    setText(pendingReplyDraft);
    textareaRef.current?.focus();
  }

  const personName = (message: ChannelMessage) => describePerson(message.sentByUserId, people, currentUserId, "A colleague");

  async function send() {
    const body = text.trim();
    if (!body || sending) return;
    setSending(true);
    setSendError(null);
    try {
      await onSend({ body, templateName: needsTemplate ? templateName.trim() : undefined, caseId: linkedCaseId, language });
      setText("");
      setNeedsTemplate(false);
    } catch (failure) {
      const message = failure instanceof Error ? failure.message : "The message was not sent.";
      setSendError(message);
      if (/template|window/i.test(message)) setNeedsTemplate(true);
      toast.error("Message not sent", { description: message });
    } finally {
      setSending(false);
    }
  }

  return (
    <div className="flex h-full min-h-0 flex-col bg-[#F7F9FC]">
      <div className="relative flex items-center gap-3 border-b border-[#E6EAF2] bg-white px-3 py-2.5 sm:px-4">
        {onBack ? (
          <button type="button" onClick={onBack} className="shrink-0 rounded-lg p-1.5 text-slate-500 hover:bg-[#F7F9FC]" aria-label="Back to conversations">
            <ChevronLeft className="h-5 w-5" />
          </button>
        ) : null}
        <div className="hidden h-10 w-10 shrink-0 items-center justify-center rounded-full bg-[#EEF4FF] text-[12px] font-bold text-[#1769FF] sm:flex">
          {conversationInitials(conversation.customer)}
        </div>
        <div className="min-w-0 flex-1">
          <p className="m-0 truncate text-[14px] font-bold text-[#0B1640]">{conversation.customer}</p>
          <p className="m-0 truncate text-[11px] text-slate-500">
            {conversation.channel}
            {conversation.customerContact ? ` · ${conversation.customerContact.replace(/\D/g, "").replace(/^(\d{2}).*(\d{2})$/, "$1***$2")}` : ""}
            {conversation.language ? ` · ${conversation.language}` : ""}
          </p>
        </div>

        {isWhatsApp && canReplyInInbox ? (
          withPerson ? (
            <div className="flex shrink-0 items-center gap-2">
              <span className="hidden items-center gap-1.5 whitespace-nowrap text-[11px] font-semibold text-sky-700 2xl:inline-flex">
                <UserRound className="h-3.5 w-3.5" aria-hidden />
                {control?.byName ? `${control.byName} is answering` : "A person is answering"}
              </span>
              <button
                type="button"
                disabled={controlSaving}
                onClick={() => void onHandBack()}
                aria-label="Hand back to Kato"
                className="inline-flex h-9 w-9 shrink-0 items-center justify-center gap-1.5 whitespace-nowrap rounded-lg border border-[#1769FF]/30 text-[11px] font-semibold text-[#1769FF] hover:bg-[#EEF4FF] disabled:opacity-50 sm:h-auto sm:w-auto sm:px-2.5 sm:py-1.5"
              >
                {controlSaving ? <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden /> : <Bot className="h-3.5 w-3.5" aria-hidden />}
                <span className="hidden sm:inline">Hand back to Kato</span>
              </button>
            </div>
          ) : (
            <div className="flex shrink-0 items-center gap-2">
              <span className="hidden items-center gap-1.5 whitespace-nowrap text-[11px] font-semibold text-emerald-700 2xl:inline-flex">
                <span className="h-1.5 w-1.5 rounded-full bg-emerald-500" aria-hidden />
                Kato is answering
              </span>
              <button
                type="button"
                disabled={controlSaving}
                onClick={() => void onTakeOver()}
                aria-label="Take over conversation"
                className="inline-flex h-9 w-9 shrink-0 items-center justify-center gap-1.5 whitespace-nowrap rounded-lg bg-[#1769FF] text-[11px] font-semibold text-white hover:bg-[#1258d6] disabled:opacity-50 sm:h-auto sm:w-auto sm:px-2.5 sm:py-1.5"
              >
                {controlSaving ? <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden /> : <UserRound className="h-3.5 w-3.5" aria-hidden />}
                <span className="hidden sm:inline">Take over</span>
              </button>
            </div>
          )
        ) : null}

        <button type="button" onClick={onRefresh} className="rounded-lg p-1.5 text-slate-500 hover:bg-[#F7F9FC]" aria-label="Refresh conversation">
          <RefreshCw className={`h-4 w-4 ${loading ? "animate-spin" : ""}`} />
        </button>
        {onOpenInfo ? (
          <button type="button" onClick={onOpenInfo} className="inline-flex shrink-0 items-center gap-1 rounded-lg border border-[#E6EAF2] px-2 py-1.5 text-[11px] font-semibold text-[#0B1640] hover:bg-[#F7F9FC]" aria-label="Open customer information">
            <Info className="h-3.5 w-3.5 text-[#1769FF]" aria-hidden />
            <span className="hidden sm:inline">Info</span>
          </button>
        ) : null}
      </div>
      {controlError ? <p className="m-0 border-b border-rose-100 bg-rose-50 px-4 py-1.5 text-[11px] text-rose-700" role="alert">{controlError}</p> : null}

      <div className="min-h-0 flex-1 overflow-y-auto px-3 py-4 sm:px-4">
        {loading && messages.length === 0 ? (
          <div className="space-y-3">
            {[...Array(4)].map((_, index) => <div key={index} className={`h-12 animate-pulse rounded-2xl bg-white ${index % 2 ? "ml-auto w-2/3" : "w-2/3"}`} />)}
          </div>
        ) : error ? (
          <p className="text-[12px] text-rose-600">{error}</p>
        ) : messages.length === 0 ? (
          <p className="text-[12px] text-slate-500">No retained messages are available for this conversation.</p>
        ) : (
          <div className="space-y-3">
            {messages.some((message) => message.isPreview) ? <p className="m-0 text-[11px] text-slate-500">Only the retained email preview is available here.</p> : null}
            {grouped.map((group) => (
              <div key={group.day}>
                <div className="mb-3 flex justify-center">
                  <span className="rounded-full bg-white px-3 py-1 text-[10px] font-semibold text-slate-500 shadow-sm">{group.day}</span>
                </div>
                <div className="space-y-2">
                  {group.items.map((message) => <Bubble key={message.messageId} message={message} personName={personName(message)} />)}
                </div>
              </div>
            ))}
            <div ref={bottomRef} />
          </div>
        )}
      </div>

      {loading && messages.length === 0 ? (
        <div className="border-t border-[#E6EAF2] bg-white px-4 py-3 text-[12px] text-slate-500">Loading conversation.</div>
      ) : canUseComposer ? (
        <div className="border-t border-[#E6EAF2] bg-white px-3 py-3 pb-[max(0.75rem,env(safe-area-inset-bottom))]">
          {pendingReplyDraft ? (
            <section aria-label="Assistant reply draft for review" className="mb-3 rounded-lg border border-[#E6EAF2] bg-[#F7F9FC] p-3 text-left">
              <p className="m-0 text-[12px] font-semibold text-[#0B1640]">Assistant reply draft · Review before sending</p>
              <p className="m-0 mt-1 whitespace-pre-wrap text-[13px] leading-relaxed text-[#35415F]">{pendingReplyDraft}</p>
              <button type="button" onClick={usePendingDraft} className="mt-2 rounded-lg border border-[#CBD5E1] bg-white px-3 py-1.5 text-[12px] font-semibold text-[#0B1640] hover:bg-[#F7F9FC]">
                Use draft
              </button>
            </section>
          ) : null}
          {isWhatsApp && !withPerson ? (
            <p className="m-0 mb-2 text-[11px] text-slate-500">
              Kato is answering. Sending a message here takes the conversation from Kato; he takes it back if you hand it over or do not reply for two hours.
            </p>
          ) : null}
          {isWhatsApp && needsTemplate ? (
            <label className="mb-2 block">
              <span className="mb-1 block text-[10px] font-semibold uppercase tracking-wide text-slate-400">The 24-hour window has ended: approved template name</span>
              <input
                value={templateName}
                onChange={(event) => setTemplateName(event.target.value)}
                placeholder="Template name"
                className="w-full rounded-xl border border-[#E6EAF2] bg-[#F7F9FC] px-3 py-2 text-[12px] text-[#0B1640] outline-none focus:border-[#1769FF]"
              />
            </label>
          ) : null}
          <div className="flex items-end gap-2">
            <span className="mb-1 inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-slate-300" title="Attachments are not supported yet" aria-hidden>
              <Paperclip className="h-4 w-4" />
            </span>
            <textarea
              ref={textareaRef}
              value={text}
              onChange={(event) => setText(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === "Enter" && !event.shiftKey) {
                  event.preventDefault();
                  void send();
                }
              }}
              disabled={sending}
              rows={1}
              maxLength={TEXT_LIMIT}
              aria-label="Message to the customer"
              placeholder="Type a message..."
              className="max-h-[180px] min-h-[44px] flex-1 resize-none overflow-y-auto rounded-2xl border border-[#E6EAF2] bg-[#F7F9FC] px-3 py-2.5 text-[14px] text-[#0B1640] outline-none focus:border-[#1769FF] focus:ring-2 focus:ring-[#1769FF]/15"
            />
            <button
              type="button"
              disabled={!text.trim() || sending || (needsTemplate && !templateName.trim())}
              onClick={() => void send()}
              className="mb-0.5 flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-[#1769FF] text-white shadow-sm hover:bg-[#1258d6] disabled:cursor-not-allowed disabled:bg-slate-300"
              aria-label="Send message"
            >
              {sending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
            </button>
          </div>
          <p className="m-0 mt-2 text-[10px] text-slate-400">
            {isWhatsApp ? "Sent as you, through the connected WhatsApp number." : "Sent by you, through the connected Telegram bot."} {text.length}/{TEXT_LIMIT}
          </p>
          {sendError ? <p className="m-0 mt-1 text-[11px] text-rose-600" role="alert">{sendError}</p> : null}
        </div>
      ) : conversation.channel === "Email" ? (
        <div className="border-t border-[#E6EAF2] bg-white px-4 py-3 text-[12px] text-slate-500">
          <Link href="/app/communications" className="font-semibold text-[#1769FF] hover:underline">Send an email in Communications</Link>
        </div>
      ) : isWhatsApp || isTelegram ? (
        <div className="border-t border-[#E6EAF2] bg-white px-4 py-3 text-[12px] text-slate-500">
          {isTelegram ? "Telegram replies are unavailable. Check the bot connection, customer consent, and your communication permissions." : "You do not have permission to reply in this conversation."}
        </div>
      ) : (
        <div className="border-t border-[#E6EAF2] bg-white px-4 py-3 text-[12px] text-slate-500">Not configured: replies are unavailable for this channel.</div>
      )}
    </div>
  );
}
