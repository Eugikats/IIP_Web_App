import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import type { Role } from "@/lib/roles";
import type { CommandCenterAnswer } from "@/lib/lovable-parity/data/commandCenterQuery";

export type ConversationSummary = {
  conversationId: string;
  title: string;
  titleSource: "system_generated" | "user_renamed";
  status: "active" | "archived";
  turnCount: number;
  createdAt: string;
  lastActivityAt: string;
  version: number;
  ownerUserId: string;
  primaryFocus: { type: string; reference: string; label: string; href: string } | null;
  grounding: ConversationGroundingSummary;
};

export type ConversationGroundingSummary = {
  historicalTimestamp: string;
  historicalVersion: string;
  currentTimestamp: string | null;
  currentVersion: string | null;
  status: "current" | "changed" | "unavailable";
  safeChangeSummary: string;
};

export type ConversationDetail = ConversationSummary & {
  turns: Array<{ turnId: string; question: string; response: CommandCenterAnswer; createdAt: string }>;
  groundingStatus: "current" | "changed" | "unavailable";
  canContinue: boolean;
};

export type ConversationList = { items: ConversationSummary[]; total: number; hasMore: boolean };

export class ConversationRequestError extends Error {
  constructor(public readonly kind: "not_found" | "unauthorized" | "conflict" | "offline" | "unavailable", message: string) {
    super(message);
    this.name = "ConversationRequestError";
  }
}

async function request<T>(path: string, role: Role, userId: string, init?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl}${path}`, {
      ...init,
      headers: { ...scopedHeaders(role, userId), ...(init?.body ? { "Content-Type": "application/json" } : {}), ...init?.headers },
    });
  } catch (reason) {
    if (reason instanceof DOMException && reason.name === "AbortError") throw reason;
    throw new ConversationRequestError("offline", "Conversations could not be reached. Check the connection and retry.");
  }
  if (response.status === 404) throw new ConversationRequestError("not_found", "Conversation not found or no longer accessible.");
  if (response.status === 401 || response.status === 403) throw new ConversationRequestError("unauthorized", "You do not have permission to view this conversation.");
  if (response.status === 409) throw new ConversationRequestError("conflict", "This conversation changed. Refresh before trying again.");
  if (!response.ok) throw new ConversationRequestError("unavailable", `Conversation request failed with HTTP ${response.status}.`);
  return response.json() as Promise<T>;
}

export function listConversations(role: Role, userId: string, archived = false, limit = 20, signal?: AbortSignal) {
  return request<ConversationList>(`/api/v1/command-center/conversations?archived=${archived}&limit=${limit}`, role, userId, { signal });
}

export function getConversation(conversationId: string, role: Role, userId: string, signal?: AbortSignal) {
  return request<ConversationDetail>(`/api/v1/command-center/conversations/${encodeURIComponent(conversationId)}`, role, userId, { signal });
}

export function archiveConversation(conversation: ConversationSummary, role: Role, userId: string) {
  return request<ConversationSummary>(`/api/v1/command-center/conversations/${encodeURIComponent(conversation.conversationId)}/archive`, role, userId, {
    method: "POST",
    body: JSON.stringify({ expectedVersion: conversation.version }),
  });
}

export function restoreConversation(conversation: ConversationSummary, role: Role, userId: string) {
  return request<ConversationSummary>(`/api/v1/command-center/conversations/${encodeURIComponent(conversation.conversationId)}/restore`, role, userId, {
    method: "POST",
    body: JSON.stringify({ expectedVersion: conversation.version }),
  });
}
