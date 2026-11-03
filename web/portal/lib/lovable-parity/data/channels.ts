"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { conversations as fixtureConversations, type Channel, type ChannelConversation } from "@/lib/lovable-parity/mock";

/**
 * Reconciliation data source for the Channels inbox.
 *
 * In LovableVisualParity mode we serve the static prototype fixtures so the
 * screen can be QA'd against the Lovable reference. Otherwise we fetch the real
 * tenant-scoped `/api/v1/channels/overview` and map it onto the same shape the
 * prototype UI already renders, so the layout is identical but the data is real,
 * and an unpopulated workspace shows an honest empty state rather than samples.
 */

export type ChannelsSource = "parity" | "live" | "empty";

export type ChannelMessage = {
  messageId: string;
  caseId?: string | null;
  direction: "Inbound" | "Outbound";
  contentType: string;
  body: string;
  deliveryState: string;
  createdAt: string;
  sentByUserId?: string | null;
  isPreview: boolean;
};

type ChannelThread = {
  channel: string;
  threadId: string;
  recipientId: string | null;
  messages: ChannelMessage[];
  canReplyInInbox: boolean;
  pendingReplyDraft?: string | null;
};

type WhatsAppSendResult = {
  deliveryState: "Received" | "Queued" | "ProviderAccepted" | "Delivered" | "Read" | "Failed" | "Unknown";
  safeError?: string | null;
};

export type SendWhatsAppInput = {
  /** A claim is optional: staff also reply to prospects who have reported nothing. */
  caseId?: string | null;
  recipientWaId: string;
  customerSafeBody: string;
  purpose: string;
  consentBasis: string;
  language: string;
  templateName?: string;
  humanApproved: boolean;
};

type OverviewRow = {
  id: string;
  customerName: string;
  channel: string;
  detectedIntent: string;
  suggestedRouting: string;
  aiConfidence: number;
  status: string;
  assignedTo?: string | null;
  lastMessage: string;
  lastActivityAt: string;
  customerContact?: string | null;
  customerLanguage?: string | null;
  languageNote?: string | null;
  controlMode?: string | null;
};

const CHANNEL_MAP: Record<string, Channel> = {
  whatsapp: "WhatsApp",
  instagram: "Instagram",
  messenger: "Messenger",
  tiktok: "TikTok",
  x: "X",
  telegram: "Telegram",
  email: "Email",
  webchat: "Web",
  website_form: "Web",
  sms: "SMS",
  broker: "Broker",
  call_center: "Teams",
};

const INTENT_MAP: Record<string, ChannelConversation["intent"]> = {
  claim_fnol: "New claim",
  claim_status: "Claim status",
  quote_request: "Quote request",
  renewal: "Quote request",
  complaint: "Complaint",
  policy_inquiry: "General",
  document_upload: "Claim update",
  payment_inquiry: "General",
  human_requested: "Wants a person",
  general_service: "General",
};

// Work states the backend emits for a conversation the assistant actually handled (see
// EfChannelReadRepository.StatusFor). Matching these exactly beats sniffing the string for
// "approval"/"review": WhatsApp delivery states are also carried in this field for threads the
// assistant never touched, and one of those must never be mistaken for a pending approval.
const STATUSES_NEEDING_A_PERSON = new Set(["awaitingapproval", "needsreview"]);

function relativeTime(iso: string): string {
  const t = Date.parse(iso);
  if (Number.isNaN(t)) return iso || "-";
  const diffMs = Date.now() - t;
  const mins = Math.round(diffMs / 60000);
  if (mins < 1) return "just now";
  if (mins < 60) return `${mins} min ago`;
  const hrs = Math.round(mins / 60);
  if (hrs < 24) return `${hrs} hr ago`;
  const days = Math.round(hrs / 24);
  return days === 1 ? "yesterday" : `${days} days ago`;
}

function mapRow(row: OverviewRow): ChannelConversation {
  const channel = CHANNEL_MAP[(row.channel || "").toLowerCase()] ?? "Unknown";
  const intent = INTENT_MAP[(row.detectedIntent || "").toLowerCase()] ?? "General";
  const status = (row.status || "").toLowerCase();
  const looksLikePhone = /^[+\d][\d\s()-]{5,}$/.test(row.customerName?.trim() ?? "");
  return {
    id: row.id,
    channel,
    customer: row.customerName || "Unknown",
    matched: Boolean(row.customerName) && !looksLikePhone && !/unknown|whatsapp contact/i.test(row.customerName)
      && !/manual case-link/i.test(row.suggestedRouting || ""),
    snippet: row.lastMessage || "",
    updated: relativeTime(row.lastActivityAt),
    unread: 0,
    needsApproval: STATUSES_NEEDING_A_PERSON.has(status.replace(/[\s_-]/g, "")),
    intent,
    customerContact: row.customerContact ?? null,
    language: row.customerLanguage ?? null,
    languageNote: row.languageNote ?? null,
    controlMode: channel === "WhatsApp" ? (row.controlMode === "Person" ? "Person" : "Kato") : undefined,
  };
}

export function useChannelConversations() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [data, setData] = useState<ChannelConversation[]>(LOVABLE_VISUAL_PARITY_MODE ? fixtureConversations : []);
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) return;
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/channels/overview`, { headers: scopedHeaders(role, "") });
      // Design language v2 §8.24 / AGENTS.md, never surface a raw provider error, stack trace, or
      // status code to the user. A thrown HTTP-status message previously reached the screen as-is.
      if (!response.ok) throw new Error("Channels failed to load.");
      const rows = (await response.json()) as OverviewRow[];
      setData(rows.map(mapRow));
    } catch {
      setError("Channels failed to load.");
      setData([]);
    } finally {
      setLoading(false);
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  const source: ChannelsSource = LOVABLE_VISUAL_PARITY_MODE ? "parity" : data.length > 0 ? "live" : "empty";
  return { conversations: data, loading, error, source, reload: load, parityMode: LOVABLE_VISUAL_PARITY_MODE };
}

export function useChannelThread(channel?: Channel | null, threadId?: string | null) {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [thread, setThread] = useState<ChannelThread | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const requestSequence = useRef(0);
  const loadedKey = useRef("");

  const load = useCallback(async () => {
    const requestId = ++requestSequence.current;
    if (!threadId || !channel || LOVABLE_VISUAL_PARITY_MODE) {
      setThread(null);
      loadedKey.current = "";
      return;
    }
    const key = `${channel}:${threadId}`;
    if (loadedKey.current !== key) {
      loadedKey.current = key;
      setThread(null);
    }
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/channels/conversations/${encodeURIComponent(channel.toLowerCase())}/${encodeURIComponent(threadId)}`, {
        headers: scopedHeaders(role, ""),
        cache: "no-store",
      });
      // §8.24 / AGENTS.md, never surface a raw provider error, stack trace, or status code.
      if (!response.ok) throw new Error("Conversation failed to load.");
      const payload = (await response.json()) as ChannelThread;
      if (requestId === requestSequence.current) setThread(payload);
    } catch {
      if (requestId === requestSequence.current) {
        setError("Conversation failed to load.");
        setThread(null);
      }
    } finally {
      if (requestId === requestSequence.current) setLoading(false);
    }
  }, [role, channel, threadId]);

  useEffect(() => { void load(); }, [load]);

  const send = useCallback(async (input: SendWhatsAppInput) => {
    if (!threadId || !thread || thread.channel !== channel?.toLowerCase()
      || thread.threadId !== threadId || !thread.canReplyInInbox || !thread.recipientId) {
      throw new Error("Replying in this conversation is not available.");
    }
    if (channel === "Telegram") {
      const response = await fetch(`${apiBaseUrl}/api/v1/channels/conversations/telegram/${encodeURIComponent(threadId)}/messages`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
        body: JSON.stringify({ body: input.customerSafeBody, idempotencyKey: crypto.randomUUID(), humanApproved: input.humanApproved }),
      });
      const payload = await response.json().catch(() => ({}));
      if (!response.ok && response.status !== 202)
        throw new Error(payload.message || payload.detail || "Telegram send failed.");
      await load();
      return payload as WhatsAppSendResult;
    }
    if (channel !== "WhatsApp" || thread?.channel !== "whatsapp")
      throw new Error("Replying in this conversation is not available.");
    const response = await fetch(`${apiBaseUrl}/api/v1/channels/whatsapp/threads/${encodeURIComponent(threadId)}/messages`, {
      method: "POST",
      headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
      body: JSON.stringify({
        ...input,
        templateName: input.templateName?.trim() || null,
        idempotencyKey: crypto.randomUUID(),
        replyToProviderMessageId: null,
        templateParameters: null,
      }),
    });
    const payload = await response.json().catch(() => ({}));
    if (!response.ok && response.status !== 202) {
      // §8.24 / AGENTS.md, a backend-supplied safe message (payload.message / payload.safeError)
      // is legitimate and kept; only the raw-status-code fallback is replaced with a generic one.
      throw new Error(payload.message || payload.safeError || "WhatsApp send failed.");
    }
    await load();
    return payload as WhatsAppSendResult;
  }, [load, role, channel, threadId, thread]);

  const selectedThread = thread?.channel === channel?.toLowerCase() && thread?.threadId === threadId ? thread : null;
  return { messages: selectedThread?.messages ?? [], recipientId: selectedThread?.recipientId ?? null, canReplyInInbox: selectedThread?.canReplyInInbox ?? false, pendingReplyDraft: selectedThread?.pendingReplyDraft ?? null, loading, error, reload: load, send };
}

export type ConversationControl = {
  mode: "Kato" | "Person";
  modeLabel: string;
  since: string | null;
  byName: string | null;
};

/** Who is answering one WhatsApp conversation, and the switch to take it or hand it back. */
export function useConversationControl(threadId?: string | null) {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [control, setControl] = useState<ConversationControl | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!threadId || LOVABLE_VISUAL_PARITY_MODE) {
      setControl(null);
      return;
    }
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/channels/whatsapp/threads/${encodeURIComponent(threadId)}/control`, { headers: scopedHeaders(role, ""), cache: "no-store" });
      setControl(response.ok ? ((await response.json()) as ConversationControl) : null);
    } catch {
      setControl(null);
    }
  }, [role, threadId]);

  useEffect(() => { void load(); }, [load]);

  const set = useCallback(async (mode: "person" | "kato") => {
    if (!threadId) return false;
    setSaving(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/channels/whatsapp/threads/${encodeURIComponent(threadId)}/control`, {
        method: "PUT",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
        body: JSON.stringify({ mode }),
      });
      if (response.status === 403) {
        setError("You do not have permission to change who answers this conversation.");
        return false;
      }
      if (!response.ok) {
        setError("The change could not be saved. Try again.");
        return false;
      }
      setControl((await response.json()) as ConversationControl);
      return true;
    } catch {
      setError("The change could not be saved. Try again.");
      return false;
    } finally {
      setSaving(false);
    }
  }, [role, threadId]);

  return { control, saving, error, reload: load, takeOver: () => set("person"), handBack: () => set("kato") };
}
