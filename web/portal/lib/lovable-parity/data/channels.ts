"use client";

import { useCallback, useEffect, useState } from "react";
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
 * prototype UI already renders — so the layout is identical but the data is real,
 * and an unpopulated workspace shows an honest empty state rather than samples.
 */

export type ChannelsSource = "parity" | "live" | "empty";

export type WhatsAppMessage = {
  messageId: string;
  caseId?: string | null;
  threadId: string;
  businessPhoneNumberId: string;
  contactWaId: string;
  direction: "Inbound" | "Outbound";
  contentType: string;
  customerSafeBody: string;
  providerMessageId?: string | null;
  templateName?: string | null;
  language?: string | null;
  deliveryState: "Received" | "Queued" | "ProviderAccepted" | "Delivered" | "Read" | "Failed" | "Unknown";
  createdAt: string;
  updatedAt: string;
  safeError?: string | null;
};

export type SendWhatsAppInput = {
  caseId: string;
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
};

const CHANNEL_MAP: Record<string, Channel> = {
  whatsapp: "WhatsApp",
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
  if (Number.isNaN(t)) return iso || "—";
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
  const channel = CHANNEL_MAP[(row.channel || "").toLowerCase()] ?? "Email";
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
      // Design language v2 §8.24 / AGENTS.md — never surface a raw provider error, stack trace, or
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

export function useWhatsAppThread(threadId?: string | null) {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [messages, setMessages] = useState<WhatsAppMessage[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!threadId || LOVABLE_VISUAL_PARITY_MODE) {
      setMessages([]);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/channels/whatsapp/threads/${encodeURIComponent(threadId)}`, {
        headers: scopedHeaders(role, ""),
        cache: "no-store",
      });
      // §8.24 / AGENTS.md — never surface a raw provider error, stack trace, or status code.
      if (!response.ok) throw new Error("WhatsApp thread failed to load.");
      setMessages((await response.json()) as WhatsAppMessage[]);
    } catch {
      setError("WhatsApp thread failed to load.");
      setMessages([]);
    } finally {
      setLoading(false);
    }
  }, [role, threadId]);

  useEffect(() => { void load(); }, [load]);

  const send = useCallback(async (input: SendWhatsAppInput) => {
    if (!threadId) throw new Error("Select a WhatsApp thread before sending.");
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
      // §8.24 / AGENTS.md — a backend-supplied safe message (payload.message / payload.safeError)
      // is legitimate and kept; only the raw-status-code fallback is replaced with a generic one.
      throw new Error(payload.message || payload.safeError || "WhatsApp send failed.");
    }
    await load();
    return payload as WhatsAppMessage;
  }, [load, role, threadId]);

  return { messages, loading, error, reload: load, send };
}
