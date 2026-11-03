"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type WordingItemKind = "Covered" | "Exclusion" | "ClaimType" | "Document" | "Field";
export type WordingItemDecision = "Pending" | "Accepted" | "Rejected";

export type WordingCitation = { quote: string; clause: string | null; page: number | null; verified: boolean };

export type WordingDraftItem = {
  itemId: string;
  kind: WordingItemKind;
  title: string;
  detail: string;
  citation: WordingCitation;
  decision: WordingItemDecision;
  edited: boolean;
  problem: string | null;
  note: string | null;
  aliases: string[];
  claimTypeNames: string[];
  required: boolean;
  documentKey: string | null;
  fieldType: string | null;
  options: string[];
};

export type WordingDraft = {
  draftId: string;
  productId: string;
  knowledgeDocumentId: string;
  documentName: string;
  status: "InReview" | "Applied" | "Discarded";
  items: WordingDraftItem[];
  provider: string;
  modelVersion: string | null;
  textTruncated: boolean;
  charactersRead: number;
  createdByUserId: string;
  createdAt: string;
  appliedByUserId: string | null;
  appliedAt: string | null;
  appliedProductVersion: number | null;
  acceptedCount: number;
  documentTypeKeys: string[];
};

export type WordingItemChange = {
  decision?: WordingItemDecision;
  title?: string;
  detail?: string;
  claimTypeNames?: string[];
  documentKey?: string;
  fieldType?: string;
  required?: boolean;
};

export type WordingApplyResult = {
  draft: WordingDraft;
  productVersion: number;
  coverageAdded: number;
  claimTypesAdded: number;
  documentsAdded: number;
  fieldsAdded: number;
  skipped: { itemId: string; title: string; reason: string }[];
};

export type WordingDocument = { knowledgeDocumentId: string; documentName: string; documentType: string; version: string; productLine: string };

export type WordingResult<T> = { ok: true; value: T } | { ok: false; error: string };

async function failure(response: Response, fallback: string): Promise<string> {
  try {
    const payload = (await response.json()) as { message?: string; errors?: Record<string, string[]> };
    const first = payload.errors ? Object.values(payload.errors).flat()[0] : null;
    return first ?? payload.message ?? fallback;
  } catch {
    return response.status === 403 ? "You do not have permission to do this." : fallback;
  }
}

async function call<T>(role: string, path: string, init: RequestInit, fallback: string): Promise<WordingResult<T>> {
  try {
    const response = await fetch(`${apiBaseUrl}${path}`, {
      ...init,
      headers: { ...scopedHeaders(role, ""), ...(init.body ? { "Content-Type": "application/json" } : {}) },
      cache: "no-store",
    });
    if (!response.ok) return { ok: false, error: await failure(response, fallback) };
    return { ok: true, value: (await response.json()) as T };
  } catch {
    return { ok: false, error: "The service could not be reached. Check your connection and try again." };
  }
}

const base = (productId: string) => `/api/v1/products/${productId}/wording-drafts`;

export function listWordings(role: string) {
  return call<WordingDocument[]>(role, "/api/v1/knowledge-documents", {}, "The wordings could not be loaded.");
}

export function listWordingDrafts(role: string, productId: string) {
  return call<WordingDraft[]>(role, base(productId), {}, "Earlier readings could not be loaded.");
}

export function createWordingDraft(role: string, productId: string, knowledgeDocumentId: string) {
  return call<WordingDraft>(role, base(productId), { method: "POST", body: JSON.stringify({ knowledgeDocumentId }) }, "The wording could not be read.");
}

export function decideWordingItem(role: string, productId: string, draftId: string, itemId: string, change: WordingItemChange) {
  return call<WordingDraft>(role, `${base(productId)}/${draftId}/items/${itemId}`, { method: "PUT", body: JSON.stringify(change) }, "That change could not be saved.");
}

export function applyWordingDraft(role: string, productId: string, draftId: string) {
  return call<WordingApplyResult>(role, `${base(productId)}/${draftId}/apply`, { method: "POST", body: JSON.stringify({}) }, "The accepted items could not be added.");
}

export function discardWordingDraft(role: string, productId: string, draftId: string) {
  return call<WordingDraft>(role, `${base(productId)}/${draftId}/discard`, { method: "POST", body: JSON.stringify({}) }, "The reading could not be discarded.");
}
